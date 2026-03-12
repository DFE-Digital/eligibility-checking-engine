using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
public class EligibilityEventsController : BaseController
{
    private readonly ILogger<EligibilityEventsController> _logger;
    private readonly IUpsertWorkingFamiliesEventUseCase _upsertUseCase;
    private readonly IDeleteWorkingFamiliesEventUseCase _deleteUseCase;

    public EligibilityEventsController(
        ILogger<EligibilityEventsController> logger,
        IAudit audit,
        IUpsertWorkingFamiliesEventUseCase upsertUseCase,
        IDeleteWorkingFamiliesEventUseCase deleteUseCase) : base(audit)
    {
        _logger = logger;
        _upsertUseCase = upsertUseCase;
        _deleteUseCase = deleteUseCase;
    }

    /// <summary>
    /// Creates or updates a Working Families eligibility event.
    /// The {id} is the HMRC-generated GUID (eligibility-event-id) that uniquely identifies the event.
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
            await _upsertUseCase.Execute(id, model);
            return Ok();
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

        try
        {
            var deleted = await _deleteUseCase.Execute(id);
            if (!deleted)
            {
                _logger.LogWarning("DELETE eligibility-events not found for id: {Id}", safeId);
                return NotFound();
            }

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
