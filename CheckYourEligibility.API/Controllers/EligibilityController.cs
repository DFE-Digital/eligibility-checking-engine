using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotFoundException = CheckYourEligibility.API.Domain.Exceptions.NotFoundException;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class EligibilityCheckController : BaseController
{
    private readonly int _bulkUploadRecordCountLimit;
    private readonly ICheckEligibilityBulkUseCase _checkEligibilityBulkUseCase;
    private readonly ICheckEligibilityForFSMUseCase _checkEligibilityForFsmUseCase;
    private readonly IGetBulkUploadProgressUseCase _getBulkUploadProgressUseCase;
    private readonly IGetBulkUploadResultsUseCase _getBulkUploadResultsUseCase;
    private readonly IGetEligibilityCheckItemUseCase _getEligibilityCheckItemUseCase;
    private readonly IGetEligibilityCheckStatusUseCase _getEligibilityCheckStatusUseCase;
    private readonly ILogger<EligibilityCheckController> _logger;
    private readonly IProcessEligibilityCheckUseCase _processEligibilityCheckUseCase;

    // Use case services
    private readonly IProcessQueueMessagesUseCase _processQueueMessagesUseCase;
    private readonly IUpdateEligibilityCheckStatusUseCase _updateEligibilityCheckStatusUseCase;

    public EligibilityCheckController(
        ILogger<EligibilityCheckController> logger,
        IAudit audit,
        IConfiguration configuration,
        IProcessQueueMessagesUseCase processQueueMessagesUseCase,
        ICheckEligibilityForFSMUseCase checkEligibilityForFsmUseCase,
        ICheckEligibilityBulkUseCase checkEligibilityBulkUseCase,
        IGetBulkUploadProgressUseCase getBulkUploadProgressUseCase,
        IGetBulkUploadResultsUseCase getBulkUploadResultsUseCase,
        IGetEligibilityCheckStatusUseCase getEligibilityCheckStatusUseCase,
        IUpdateEligibilityCheckStatusUseCase updateEligibilityCheckStatusUseCase,
        IProcessEligibilityCheckUseCase processEligibilityCheckUseCase,
        IGetEligibilityCheckItemUseCase getEligibilityCheckItemUseCase)
        : base(audit)
    {
        _logger = logger;
        _bulkUploadRecordCountLimit = configuration.GetValue<int>("BulkEligibilityCheckLimit");

        // Initialize use cases
        _processQueueMessagesUseCase = processQueueMessagesUseCase;
        _checkEligibilityForFsmUseCase = checkEligibilityForFsmUseCase;
        _checkEligibilityBulkUseCase = checkEligibilityBulkUseCase;
        _getBulkUploadProgressUseCase = getBulkUploadProgressUseCase;
        _getBulkUploadResultsUseCase = getBulkUploadResultsUseCase;
        _getEligibilityCheckStatusUseCase = getEligibilityCheckStatusUseCase;
        _updateEligibilityCheckStatusUseCase = updateEligibilityCheckStatusUseCase;
        _processEligibilityCheckUseCase = processEligibilityCheckUseCase;
        _getEligibilityCheckItemUseCase = getEligibilityCheckItemUseCase;
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
        var result = await _processQueueMessagesUseCase.Execute(queue);

        if (result.Data == "Invalid Request.")
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = result.Data }] });

        return new OkObjectResult(result);
    }

    /// <summary>
    ///     Posts a FSM Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>If the check has already been submitted, then the stored Hash is returned</remarks>
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check/free-school-meals")]
    [HttpPost("/check/early-year-pupil-premium")]
    [HttpPost("/check/two-year-offer")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibility([FromBody] CheckEligibilityRequest_Fsm model)
    {
        try
        {
            // Ensure we have a Data object
            if (model.Data == null)
            {
                model.Data = new CheckEligibilityRequestData_Fsm();
            }

            if (HttpContext.Request.Path.Value.EndsWith("/two-year-offer"))
            {
                model.Data.Type = CheckEligibilityType.TwoYearOffer;
            }
            else if (HttpContext.Request.Path.Value.EndsWith("/early-year-pupil-premium"))
            {
                model.Data.Type = CheckEligibilityType.EarlyYearPupilPremium;
            }
            else
            {
                model.Data.Type = CheckEligibilityType.FreeSchoolMeals;
            }
            
            var result = await _checkEligibilityForFsmUseCase.Execute(model);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Posts the array of FSM checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/free-school-meals")]
    [HttpPost("/bulk-check/early-year-pupil-premium")]
    [HttpPost("/bulk-check/two-year-offer")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> CheckEligibilityBulk([FromBody] CheckEligibilityRequestBulk_Fsm model)
    {
        try
        {
            CheckEligibilityType eligibilityType = CheckEligibilityType.FreeSchoolMeals;
            
            if (HttpContext.Request.Path.Value.EndsWith("/two-year-offer"))
            {
                eligibilityType = CheckEligibilityType.TwoYearOffer;
            }
            else if (HttpContext.Request.Path.Value.EndsWith("/early-year-pupil-premium"))
            {
                eligibilityType = CheckEligibilityType.EarlyYearPupilPremium;
            }

            if (model.Data == null)
            {
                model.Data = new List<CheckEligibilityRequestData_Fsm>();
            }
            else
            {
                // Set the type for each item in the collection
                // This ensures each item uses the correct eligibility type based on the endpoint URL
                foreach (var item in model.Data)
                {
                    if (item != null)
                    {
                        item.Type = eligibilityType;
                    }
                }
            }
            
            var result = await _checkEligibilityBulkUseCase.Execute(model, _bulkUploadRecordCountLimit);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Bulk Upload status
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityBulkStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/bulk-check/{guid}/progress")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> BulkUploadProgress(string guid)
    {
        try
        {
            var result = await _getBulkUploadProgressUseCase.Execute(guid);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }

        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Loads results of bulk loads given a group Id
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityBulkResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/bulk-check/{guid}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> BulkUploadResults(string guid)
    {
        try
        {
            var result = await _getBulkUploadResultsUseCase.Execute(guid);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }

        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid, Status = "404" }] });
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Gets an FSM an Eligibility Check status
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/check/{guid}/status")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityStatus(string guid)
    {
        try
        {
            var result = await _getEligibilityCheckStatusUseCase.Execute(guid);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }

        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
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

        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "" }] });
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

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
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
        catch (ProcessCheckException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }
    }

    /// <summary>
    ///     Gets an Eligibility check using the supplied GUID
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityItemResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/check/{guid}")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> EligibilityCheck(string guid)
    {
        try
        {
            var result = await _getEligibilityCheckItemUseCase.Execute(guid);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }

        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }
}