using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FeatureManagement.Domain.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotFoundException = CheckYourEligibility.API.Domain.Exceptions.NotFoundException;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class EligibilityCheckController : BaseController
{
    private readonly int _bulkUploadRecordCountLimit;
    private readonly ICheckEligibilityBulkUseCase<CheckEligibilityRequestBulk_Fsm, CheckEligibilityRequestBulkData_Fsm> _checkEligibilityBulkUseCase;
    private readonly ICheckEligibilityBulkUseCase<CheckEligibilityRequestBulk_2yo, CheckEligibilityRequestBulkData_2yo> _checkEligibilityBulkUseCase_2yo;
    private readonly ICheckEligibilityBulkUseCase<CheckEligibilityRequestBulk_Eypp, CheckEligibilityRequestBulkData_Eypp> _checkEligibilityBulkUseCase_Eypp;
    private readonly ICheckEligibilityForFSMUseCase _checkEligibilityForFsmUseCase;
    private readonly ICheckEligibilityFor2yoUseCase _checkEligibilityFor2yoUseCase;
    private readonly ICheckEligibilityForEyppUseCase _checkEligibilityForEyppUseCase;
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
        ICheckEligibilityFor2yoUseCase checkEligibilityFor2yoUseCase,
        ICheckEligibilityForEyppUseCase checkEligibilityForEyppUseCase,
        ICheckEligibilityForFSMUseCase checkEligibilityForFsmUseCase,
        ICheckEligibilityBulkUseCase<CheckEligibilityRequestBulk_Fsm, CheckEligibilityRequestBulkData_Fsm> checkEligibilityBulkUseCase,
        ICheckEligibilityBulkUseCase<CheckEligibilityRequestBulk_2yo, CheckEligibilityRequestBulkData_2yo> checkEligibilityBulkUseCase_2yo,
        ICheckEligibilityBulkUseCase<CheckEligibilityRequestBulk_Eypp, CheckEligibilityRequestBulkData_Eypp> checkEligibilityBulkUseCase_Eypp,
        IGetBulkUploadProgressUseCase getBulkUploadProgressUseCase,
        IGetBulkUploadResultsUseCase getBulkUploadResultsUseCase,
        IGetEligibilityCheckStatusUseCase getEligibilityCheckStatusUseCase,
        IUpdateEligibilityCheckStatusUseCase updateEligibilityCheckStatusUseCase,
        IProcessEligibilityCheckUseCase processEligibilityCheckUseCase,
        IGetEligibilityCheckItemUseCase getEligibilityCheckItemUseCase
        )
        : base(audit)
    {
        _logger = logger;
        _bulkUploadRecordCountLimit = configuration.GetValue<int>("BulkEligibilityCheckLimit");

        // Initialize use cases
        _processQueueMessagesUseCase = processQueueMessagesUseCase;
        _checkEligibilityForFsmUseCase = checkEligibilityForFsmUseCase;
        _checkEligibilityFor2yoUseCase = checkEligibilityFor2yoUseCase;
        _checkEligibilityForEyppUseCase = checkEligibilityForEyppUseCase;
        _checkEligibilityBulkUseCase = checkEligibilityBulkUseCase;
        _checkEligibilityBulkUseCase_2yo = checkEligibilityBulkUseCase_2yo;
        _checkEligibilityBulkUseCase_Eypp = checkEligibilityBulkUseCase_Eypp;
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
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityFsm([FromBody] CheckEligibilityRequest_Fsm model)
    {
        try
        {
            model.Data ??= new CheckEligibilityRequestData_Fsm();
            
            var result = await _checkEligibilityForFsmUseCase.Execute(model);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Posts a 2YO Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>If the check has already been submitted, then the stored Hash is returned</remarks>
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check/two-year-offer")]

    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibility2yo([FromBody] CheckEligibilityRequest_2yo model)
    {
        try
        {
            model.Data ??= new CheckEligibilityRequestData_2yo();

            var result = await _checkEligibilityFor2yoUseCase.Execute(model);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Posts a EYPP Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>If the check has already been submitted, then the stored Hash is returned</remarks>
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check/early-year-pupil-premium")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityEypp([FromBody] CheckEligibilityRequest_Eypp model)
    {
        try
        {
            model.Data ??= new CheckEligibilityRequestData_Eypp();

            var result = await _checkEligibilityForEyppUseCase.Execute(model);

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
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> CheckEligibilityBulkFsm([FromBody] CheckEligibilityRequestBulk_Fsm model)
    {
        try
        {
            model.Data ??= new List<CheckEligibilityRequestBulkData_Fsm>();
            
            var result = await _checkEligibilityBulkUseCase.Execute(model, _bulkUploadRecordCountLimit);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }


    /// <summary>
    ///     Posts the array of 2YO checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/two-year-offer")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> CheckEligibilityBulk2yo([FromBody] CheckEligibilityRequestBulk_2yo model)
    {
        try
        {            
            model.Data ??= new List<CheckEligibilityRequestBulkData_2yo>();

            var result = await _checkEligibilityBulkUseCase_2yo.Execute(model, _bulkUploadRecordCountLimit);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }

        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
    }

    /// <summary>
    ///     Posts the array of EYPP checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/early-year-pupil-premium")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> CheckEligibilityBulkEypp([FromBody] CheckEligibilityRequestBulk_Eypp model)
    {
        try
        {
            model.Data ??= new List<CheckEligibilityRequestBulkData_Eypp>();

            var result = await _checkEligibilityBulkUseCase_Eypp.Execute(model, _bulkUploadRecordCountLimit);

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