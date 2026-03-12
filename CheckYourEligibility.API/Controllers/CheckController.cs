using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;
using NotFoundException = CheckYourEligibility.API.Domain.Exceptions.NotFoundException;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class CheckController : BaseController
{
    private readonly ICheckEligibilityUseCase _checkEligibilityUseCase;
    private readonly IGetEligibilityCheckItemUseCase _getEligibilityCheckItemUseCase;
    private readonly IGetEligibilityCheckStatusUseCase _getEligibilityCheckStatusUseCase;
    private readonly ILogger<CheckController> _logger;

    public CheckController(
        ILogger<CheckController> logger,
        IAudit audit,
        ICheckEligibilityUseCase checkEligibilityUseCase,
        IGetEligibilityCheckStatusUseCase getEligibilityCheckStatusUseCase,
        IGetEligibilityCheckItemUseCase getEligibilityCheckItemUseCase
    )
        : base(audit)
    {
        _logger = logger;
        
        _checkEligibilityUseCase = checkEligibilityUseCase;
        _getEligibilityCheckStatusUseCase = getEligibilityCheckStatusUseCase;
        _getEligibilityCheckItemUseCase = getEligibilityCheckItemUseCase;
    }

    /// <summary>
    ///     Posts a FSM Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>
    /// If the check has already been submitted, then the stored Hash is returned.
    /// The check type is determined by the endpoint path (/check/free-school-meals).
    /// The 'type' field in the request body is optional and can be omitted.
    /// </remarks>
    [SwaggerRequestExample(typeof(CheckEligibilityRequest<CheckEligibilityRequestData>), typeof(CheckFSMModelExample))]
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check/free-school-meals")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityFsm(
        [FromBody] CheckEligibilityRequest<CheckEligibilityRequestData> model)
    {
        try
        {
            var result = await _checkEligibilityUseCase.Execute(model, CheckEligibilityType.FreeSchoolMeals);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
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
    ///     Posts a 2YO Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>
    /// If the check has already been submitted, then the stored Hash is returned.
    /// The check type is determined by the endpoint path (/check/two-year-offer).
    /// The 'type' field in the request body is optional and can be omitted.
    /// </remarks>
    [SwaggerRequestExample(typeof(CheckEligibilityRequest<CheckEligibilityRequestData>), typeof(Check2YOModelExample))]
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check/two-year-offer")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibility2yo(
        [FromBody] CheckEligibilityRequest<CheckEligibilityRequestData> model)
    {
        try
        {
            var result = await _checkEligibilityUseCase.Execute(model, CheckEligibilityType.TwoYearOffer);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
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
    ///     Posts a EYPP Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>
    /// If the check has already been submitted, then the stored Hash is returned.
    /// The check type is determined by the endpoint path (/check/early-year-pupil-premium).
    /// The 'type' field in the request body is optional and can be omitted.
    /// </remarks>
    [SwaggerRequestExample(typeof(CheckEligibilityRequest<CheckEligibilityRequestData>), typeof(CheckEYPPModelExample))]
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check/early-year-pupil-premium")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityEypp(
        [FromBody] CheckEligibilityRequest<CheckEligibilityRequestData> model)
    {
        try
        {
            var result = await _checkEligibilityUseCase.Execute(model, CheckEligibilityType.EarlyYearPupilPremium);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
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
    /// Posts a WF Eligibility Check to the processing queue
    /// </summary>
    /// <param name="model"></param>
    /// <remarks>
    /// If the check has already been submitted, then the stored Hash is returned.
    /// The check type is determined by the endpoint path (/check/working-families).
    /// The 'type' field in the request body is optional and can be omitted.
    /// </remarks> 
    [SwaggerRequestExample(typeof(CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>),
        typeof(CheckWFModelExample))]
    [ProducesResponseType(typeof(CheckEligibilityResponse), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [HttpPost("/check/working-families")]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityWF(
        [FromBody] CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData> model)
    {
        try
        {
            var result = await _checkEligibilityUseCase.Execute(model, CheckEligibilityType.WorkingFamilies);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
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
            var result = await _getEligibilityCheckStatusUseCase.Execute(guid, CheckEligibilityType.None);

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
    }

    /// <summary>
    ///     Gets an FSM an Eligibility Check status
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/check/{type}/{guid}/status")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> CheckEligibilityStatus(CheckEligibilityType type, string guid)
    {
        try
        {
            var result = await _getEligibilityCheckStatusUseCase.Execute(guid, type);

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
            var result = await _getEligibilityCheckItemUseCase.Execute(guid, CheckEligibilityType.None);
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
    }

    /// <summary>
    ///     Gets an Eligibility check of the given type using the supplied GUID
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityItemResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/check/{type}/{guid}")]
    [Authorize(Policy = PolicyNames.RequireCheckScope)]
    public async Task<ActionResult> EligibilityCheck(CheckEligibilityType type, string guid)
    {
        try
        {
            var result = await _getEligibilityCheckItemUseCase.Execute(guid, type);
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
    }
}