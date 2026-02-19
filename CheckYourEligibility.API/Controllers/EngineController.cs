using Azure;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using NotFoundException = CheckYourEligibility.API.Domain.Exceptions.NotFoundException;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class EngineController : BaseController
{
    private readonly ILogger<EngineController> _logger;
    private readonly IProcessEligibilityCheckUseCase _processEligibilityCheckUseCase;

    // Use case services
    private readonly IProcessEligibilityBulkCheckUseCase _processEligibilityBulkCheckUseCase;
    private readonly IUpdateEligibilityCheckStatusUseCase _updateEligibilityCheckStatusUseCase;

    public EngineController(
        ILogger<EngineController> logger,
        IAudit audit,
        IProcessEligibilityBulkCheckUseCase processEligibilityBulkCheckUseCase,
        IUpdateEligibilityCheckStatusUseCase updateEligibilityCheckStatusUseCase,
        IProcessEligibilityCheckUseCase processEligibilityCheckUseCase
    )
        : base(audit)
    {
        _logger = logger;
        _processEligibilityBulkCheckUseCase = processEligibilityBulkCheckUseCase;
        _updateEligibilityCheckStatusUseCase = updateEligibilityCheckStatusUseCase;
        _processEligibilityCheckUseCase = processEligibilityCheckUseCase;
    }

    /// <summary>
    ///     Processes check messages on the specified queue
    /// </summary>
    /// <param name="queue"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(MessageResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/engine/process")]
    [Authorize(Policy = PolicyNames.RequireEngineScope)]
    public async Task<ActionResult> ProcessQueue(string queue)
    {
        var result = await _processEligibilityBulkCheckUseCase.Execute(queue);

        if (result.Data == "Invalid Request.")
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = result.Data }] });

        return new OkObjectResult(result);
    }

    /// <summary>
    ///     Updates an Eligibility check status
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/engine/check/{guid}/status")]
    [Authorize(Policy = PolicyNames.RequireEngineScope)]
    public async Task<ActionResult> EligibilityCheckStatusUpdate(string guid,
        [FromBody] EligibilityStatusUpdateRequest model)
    {
        try
        {
            var result = await _updateEligibilityCheckStatusUseCase.Execute(guid, model);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }

        catch (NotFoundException)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "" }] });
        }

        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Processes FSM an Eligibility Check producing an outcome status
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    /// <remarks>If a dependent Gateway, ie DWP fails then the status is not updated</remarks>
    [ProducesResponseType(typeof(CheckEligibilityStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(CheckEligibilityStatusResponse), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPut("/engine/process/{guid}")]
    [Authorize(Policy = PolicyNames.RequireEngineScope)]
    public async Task<ActionResult> Process(string guid)
    {
        try
        {
            var result = await _processEligibilityCheckUseCase.Execute(guid);
            
            if ((CheckEligibilityStatus)Enum.Parse(typeof(CheckEligibilityStatus), result.Data.Status) == CheckEligibilityStatus.queuedForProcessing)
            {
              return StatusCode(StatusCodes.Status503ServiceUnavailable,
              new ErrorResponse { Errors = [new Error { Title = "Eligibility check still queued for processing" }] });

            }
            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }

        catch (ApplicationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ProcessCheckException)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }
    }
}