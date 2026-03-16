using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Filters;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[ServiceFilter(typeof(ClientCertificateValidationFilter))]
public class EligibilityEventsController : BaseController
{
    private readonly ILogger<EligibilityEventsController> _logger;
    private readonly IUpsertWorkingFamiliesEventUseCase _upsertUseCase;
    private readonly IDeleteWorkingFamiliesEventUseCase _deleteUseCase;
    private readonly IEcsEligibilityEventsAdapter? _ecsAdapter;
    private readonly IConfiguration _configuration;

    public EligibilityEventsController(
        ILogger<EligibilityEventsController> logger,
        IAudit audit,
        IConfiguration configuration,
        IUpsertWorkingFamiliesEventUseCase upsertUseCase,
        IDeleteWorkingFamiliesEventUseCase deleteUseCase,
        IEcsEligibilityEventsAdapter? ecsAdapter = null) : base(audit)
    {
        _logger = logger;
        _configuration = configuration;
        _upsertUseCase = upsertUseCase;
        _deleteUseCase = deleteUseCase;
        _ecsAdapter = ecsAdapter;
    }

    /// <summary>
    /// Creates or updates a Working Families eligibility event.
    /// The {id} is the HMRC-generated GUID (eligibility-event-id) that uniquely identifies the event.
    /// Forwards the request to ECS first; only persists locally on ECS success.
    /// Returns 409 Conflict if the same id is received with a different DERN.
    /// </summary>
    /// <param name="id">HMRC-supplied eligibility event identifier.</param>
    /// <param name="model">The eligibility event request body.</param>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    [Consumes("application/json")]
    [HttpPut("/efe/api/v1/eligibility-events/{id}")]
    public async Task<IActionResult> EligibilityEvents(string id, [FromBody] EligibilityEventRequest model)
    {
        var safeId = id?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _logger.LogInformation("PUT eligibility-events request received for id: {Id}", safeId);
        var requestStopwatch = Stopwatch.StartNew();

        if (!Guid.TryParse(id, out _))
        {
            _logger.LogWarning("PUT eligibility-events invalid GUID format for id: {Id}", safeId);
            return BadRequest(new { error = "id must be a valid GUID" });
        }

        if (!ModelState.IsValid || model?.EligibilityEvent == null)
        {
            _logger.LogWarning("PUT eligibility-events bad request for id: {Id}", safeId);
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage ?? "Bad Request";
            return BadRequest(new { error = firstError });
        }

        try
        {
            // Forward to ECS via Barracuda first (skip when ForwardToEcs is false)
            var forwardToEcs = _configuration.GetValue<bool>("Ecs:EligibilityEvents:ForwardToEcs");
            if (forwardToEcs && _ecsAdapter != null)
            {
                HttpResponseMessage ecsResponse;
                var ecsStopwatch = Stopwatch.StartNew();
                try
                {
                    ecsResponse = await _ecsAdapter.ForwardPutAsync(id, model);
                }
                catch (HttpRequestException ex)
                {
                    ecsStopwatch.Stop();
                    _logger.LogError(ex, "PUT eligibility-events failed to reach ECS for id: {Id}, EcsElapsedMs: {EcsElapsedMs}", safeId, ecsStopwatch.ElapsedMilliseconds);
                    return StatusCode(502, new { error = "Failed to forward request to ECS" });
                }
                catch (TaskCanceledException ex)
                {
                    ecsStopwatch.Stop();
                    _logger.LogError(ex, "PUT eligibility-events ECS request timed out for id: {Id}, EcsElapsedMs: {EcsElapsedMs}", safeId, ecsStopwatch.ElapsedMilliseconds);
                    return StatusCode(502, new { error = "ECS request timed out" });
                }

                ecsStopwatch.Stop();
                _logger.LogInformation("PUT eligibility-events ECS responded for id: {Id}, EcsStatusCode: {EcsStatusCode}, EcsElapsedMs: {EcsElapsedMs}", safeId, (int)ecsResponse.StatusCode, ecsStopwatch.ElapsedMilliseconds);

                if (!ecsResponse.IsSuccessStatusCode)
                {
                    var ecsBody = await ecsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "PUT eligibility-events ECS returned non-success for id: {Id}, StatusCode: {StatusCode}, Body: {Body}",
                        safeId, (int)ecsResponse.StatusCode, ecsBody);
                    return new ContentResult
                    {
                        StatusCode = (int)ecsResponse.StatusCode,
                        Content = JsonConvert.SerializeObject(new { error = $"ECS returned {(int)ecsResponse.StatusCode}", detail = ecsBody }),
                        ContentType = "application/json"
                    };
                }
            }

            // ECS succeeded (or adapter not configured) — persist locally
            await _upsertUseCase.Execute(id, model);
            requestStopwatch.Stop();
            _logger.LogInformation("PUT eligibility-events completed for id: {Id}, TotalElapsedMs: {TotalElapsedMs}", safeId, requestStopwatch.ElapsedMilliseconds);
            return Ok();
        }
        catch (DernOverlapException ex)
        {
            _logger.LogWarning("PUT eligibility-events overlap for id: {Id} — DERN {Dern} dates overlap", safeId, ex.Dern);
            var detail = JsonConvert.SerializeObject(new
            {
                EligibilityEvent = id,
                ex.Dern,
                ex.Overlaps,
                error = ex.Message
            });
            return BadRequest(new { error = "ECE returned 400", detail });
        }
        catch (InvalidOperationException ex) when (ex.Message == "CONFLICT")
        {
            _logger.LogWarning("PUT eligibility-events conflict for id: {Id} — DERN mismatch", safeId);
            return Conflict(new { error = "Eligibility Event has a different DERN from previous request with same id" });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("PUT eligibility-events validation error for id: {Id}: {Message}", safeId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            var logId = Guid.NewGuid().ToString();
            _logger.LogError(ex, "PUT eligibility-events unexpected error for id: {Id}, logId: {LogId}", safeId, logId);
            return new ContentResult { Content = logId, ContentType = "text/plain", StatusCode = (int)HttpStatusCode.InternalServerError };
        }
    }

    /// <summary>
    /// Soft-deletes a Working Families eligibility event by the HMRC-supplied id.
    /// Forwards the request to ECS first; only persists locally on ECS success.
    /// Returns 404 if the event does not exist or has already been deleted.
    /// </summary>
    /// <param name="id">HMRC-supplied eligibility event identifier.</param>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [HttpDelete("/efe/api/v1/eligibility-events/{id}")]
    public async Task<IActionResult> DeleteEligibilityEvent(string id)
    {
        var safeId = id?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _logger.LogInformation("DELETE eligibility-events request received for id: {Id}", safeId);
        var requestStopwatch = Stopwatch.StartNew();

        if (!Guid.TryParse(id, out _))
        {
            _logger.LogWarning("DELETE eligibility-events invalid GUID format for id: {Id}", safeId);
            return BadRequest(new { error = "id must be a valid GUID" });
        }

        try
        {
            // Forward to ECS via Barracuda first (skip when ForwardToEcs is false)
            var forwardToEcs = _configuration.GetValue<bool>("Ecs:EligibilityEvents:ForwardToEcs");
            if (forwardToEcs && _ecsAdapter != null)
            {
                HttpResponseMessage ecsResponse;
                var ecsStopwatch = Stopwatch.StartNew();
                try
                {
                    ecsResponse = await _ecsAdapter.ForwardDeleteAsync(id);
                }
                catch (HttpRequestException ex)
                {
                    ecsStopwatch.Stop();
                    _logger.LogError(ex, "DELETE eligibility-events failed to reach ECS for id: {Id}, EcsElapsedMs: {EcsElapsedMs}", safeId, ecsStopwatch.ElapsedMilliseconds);
                    return StatusCode(502, new { error = "Failed to forward request to ECS" });
                }
                catch (TaskCanceledException ex)
                {
                    ecsStopwatch.Stop();
                    _logger.LogError(ex, "DELETE eligibility-events ECS request timed out for id: {Id}, EcsElapsedMs: {EcsElapsedMs}", safeId, ecsStopwatch.ElapsedMilliseconds);
                    return StatusCode(502, new { error = "ECS request timed out" });
                }

                ecsStopwatch.Stop();
                _logger.LogInformation("DELETE eligibility-events ECS responded for id: {Id}, EcsStatusCode: {EcsStatusCode}, EcsElapsedMs: {EcsElapsedMs}", safeId, (int)ecsResponse.StatusCode, ecsStopwatch.ElapsedMilliseconds);

                if (!ecsResponse.IsSuccessStatusCode)
                {
                    var ecsBody = await ecsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "DELETE eligibility-events ECS returned non-success for id: {Id}, StatusCode: {StatusCode}, Body: {Body}",
                        safeId, (int)ecsResponse.StatusCode, ecsBody);
                    return new ContentResult
                    {
                        StatusCode = (int)ecsResponse.StatusCode,
                        Content = JsonConvert.SerializeObject(new { error = $"ECS returned {(int)ecsResponse.StatusCode}", detail = ecsBody }),
                        ContentType = "application/json"
                    };
                }
            }

            // ECS succeeded (or adapter not configured) — persist locally
            var deleted = await _deleteUseCase.Execute(id);
            if (!deleted)
            {
                _logger.LogWarning("DELETE eligibility-events not found for id: {Id}", safeId);
                return NotFound();
            }

            requestStopwatch.Stop();
            _logger.LogInformation("DELETE eligibility-events completed for id: {Id}, TotalElapsedMs: {TotalElapsedMs}", safeId, requestStopwatch.ElapsedMilliseconds);
            return Ok();
        }
        catch (Exception ex)
        {
            var logId = Guid.NewGuid().ToString();
            _logger.LogError(ex, "DELETE eligibility-events unexpected error for id: {Id}, logId: {LogId}", safeId, logId);
            return new ContentResult { Content = logId, ContentType = "text/plain", StatusCode = (int)HttpStatusCode.InternalServerError };
        }
    }
}
