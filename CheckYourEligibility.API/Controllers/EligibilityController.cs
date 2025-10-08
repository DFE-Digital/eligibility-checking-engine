using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;
using System.Net;
using NotFoundException = CheckYourEligibility.API.Domain.Exceptions.NotFoundException;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class EligibilityCheckController : BaseController
{
    private readonly int _bulkUploadRecordCountLimit;
    private readonly ICheckEligibilityUseCase _checkEligibilityUseCase;
    private readonly ICheckEligibilityBulkUseCase _checkEligibilityBulkUseCase;
    private readonly IGetBulkCheckStatusesUseCase _getBulkCheckStatusesUseCase;
    private readonly IGetBulkUploadProgressUseCase _getBulkUploadProgressUseCase;
    private readonly IGetBulkUploadResultsUseCase _getBulkUploadResultsUseCase;
    private readonly IGetEligibilityCheckItemUseCase _getEligibilityCheckItemUseCase;
    private readonly IGetEligibilityCheckStatusUseCase _getEligibilityCheckStatusUseCase;
    private readonly IDeleteBulkCheckUseCase _deleteBulkUploadUseCase;
    private readonly IGetAllBulkChecksUseCase _getAllBulkChecksUseCase;
    private readonly ILogger<EligibilityCheckController> _logger;
    private readonly IProcessEligibilityCheckUseCase _processEligibilityCheckUseCase;
    private readonly string _localAuthorityScopeName;
    private readonly string _multiAcademyTrustScopeName;

    // Use case services
    private readonly IProcessQueueMessagesUseCase _processQueueMessagesUseCase;
    private readonly IUpdateEligibilityCheckStatusUseCase _updateEligibilityCheckStatusUseCase;

    public EligibilityCheckController(
        ILogger<EligibilityCheckController> logger,
        IAudit audit,
        IConfiguration configuration,
        IProcessQueueMessagesUseCase processQueueMessagesUseCase,
        ICheckEligibilityUseCase checkEligibilityUseCase,
        ICheckEligibilityBulkUseCase checkEligibilityBulkUseCase,
        IGetBulkCheckStatusesUseCase getBulkCheckStatusesUseCase,
        IGetBulkUploadProgressUseCase getBulkUploadProgressUseCase,
        IGetBulkUploadResultsUseCase getBulkUploadResultsUseCase,
        IGetEligibilityCheckStatusUseCase getEligibilityCheckStatusUseCase,
        IUpdateEligibilityCheckStatusUseCase updateEligibilityCheckStatusUseCase,
        IProcessEligibilityCheckUseCase processEligibilityCheckUseCase,
        IGetEligibilityCheckItemUseCase getEligibilityCheckItemUseCase,
        IDeleteBulkCheckUseCase deleteBulkUploadUseCase,
        IGetAllBulkChecksUseCase getAllBulkChecksUseCase
    )
        : base(audit)
    {
        _logger = logger;
        _bulkUploadRecordCountLimit = configuration.GetValue<int>("BulkEligibilityCheckLimit");
        _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
        _multiAcademyTrustScopeName = configuration.GetValue<string>("Jwt:Scopes:multi_academy_trust") ?? "multi_academy_trust";

        // Initialize use cases
        _processQueueMessagesUseCase = processQueueMessagesUseCase;
        _checkEligibilityUseCase = checkEligibilityUseCase;
        _checkEligibilityBulkUseCase = checkEligibilityBulkUseCase;
        _getBulkCheckStatusesUseCase = getBulkCheckStatusesUseCase;
        _getBulkUploadProgressUseCase = getBulkUploadProgressUseCase;
        _getBulkUploadResultsUseCase = getBulkUploadResultsUseCase;
        _getEligibilityCheckStatusUseCase = getEligibilityCheckStatusUseCase;
        _updateEligibilityCheckStatusUseCase = updateEligibilityCheckStatusUseCase;
        _processEligibilityCheckUseCase = processEligibilityCheckUseCase;
        _getEligibilityCheckItemUseCase = getEligibilityCheckItemUseCase;
        _deleteBulkUploadUseCase = deleteBulkUploadUseCase;
        _getAllBulkChecksUseCase = getAllBulkChecksUseCase;
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
    /// <remarks>If the check has already been submitted, then the stored Hash is returned</remarks>
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
    /// <remarks>If the check has already been submitted, then the stored Hash is returned</remarks>
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
    /// <remarks>If the check has already been submitted, then the stored Hash is returned</remarks> 
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
    ///     Posts the array of WF checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/working-families")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CheckEligibilityBulkWF([FromBody] CheckEligibilityRequestWorkingFamiliesBulk model)
    {
        try
        {
            // Extract local authority IDs from user claims
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            // Set LocalAuthorityId if not provided and user has access to only one LA
            if (!model.LocalAuthorityId.HasValue && localAuthorityIds.Count == 1 && localAuthorityIds[0] != 0)
            {
                model.LocalAuthorityId = localAuthorityIds[0];
            }

            var result = await _checkEligibilityBulkUseCase.Execute(model, CheckEligibilityType.WorkingFamilies,
                _bulkUploadRecordCountLimit);
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
    ///     Posts the array of FSM checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/free-school-meals")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CheckEligibilityBulkFsm([FromBody] CheckEligibilityRequestBulk model)
    {
        try
        {
            string userOrganisationType = User.UserOrgType();

            if (string.IsNullOrEmpty(userOrganisationType)) {

                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No multi-academy trust or local authority scope found." }]
                });

            }
            if (userOrganisationType == _localAuthorityScopeName || userOrganisationType == _multiAcademyTrustScopeName)
            {

                // Extract local authority IDs from user claims
                var orgIds = User.GetSpecificScopeIds(userOrganisationType);

                // Set LocalAuthorityId if not provided and user has access to only one LA
                if (!model.LocalAuthorityId.HasValue && orgIds.Count == 1 && orgIds[0] != 0)
                {
                    model.LocalAuthorityId = orgIds[0];
                }

            }                 
            var result = await _checkEligibilityBulkUseCase.Execute(model, CheckEligibilityType.FreeSchoolMeals,
                _bulkUploadRecordCountLimit);
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
    ///     Posts the array of 2YO checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/two-year-offer")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CheckEligibilityBulk2yo([FromBody] CheckEligibilityRequestBulk model)
    {
        try
        {
            // Extract local authority IDs from user claims
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            // Set LocalAuthorityId if not provided and user has access to only one LA
            if (!model.LocalAuthorityId.HasValue && localAuthorityIds.Count == 1 && localAuthorityIds[0] != 0)
            {
                model.LocalAuthorityId = localAuthorityIds[0];
            }

            var result = await _checkEligibilityBulkUseCase.Execute(model, CheckEligibilityType.TwoYearOffer,
                _bulkUploadRecordCountLimit);
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
    ///     Posts the array of EYPP checks
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/early-year-pupil-premium")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CheckEligibilityBulkEypp([FromBody] CheckEligibilityRequestBulk model)
    {
        try
        {
            // Extract local authority IDs from user claims
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            // Set LocalAuthorityId if not provided and user has access to only one LA
            if (!model.LocalAuthorityId.HasValue && localAuthorityIds.Count == 1 && localAuthorityIds[0] != 0)
            {
                model.LocalAuthorityId = localAuthorityIds[0];
            }

            var result = await _checkEligibilityBulkUseCase.Execute(model, CheckEligibilityType.EarlyYearPupilPremium,
                _bulkUploadRecordCountLimit);
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
    ///     Bulk Upload status
    /// </summary>
    /// <param name="organisationId"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityBulkStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/bulk-check/status/{organisationId}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> BulkCheckStatuses(string organisationId)
    {
        try
        {
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var localAuthority = organisationId; // HttpContext.User.GetLocalAuthorityId("local_authority");

            var result = await _getBulkCheckStatusesUseCase.Execute(localAuthority, localAuthorityIds);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "Not Found" }] });
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
    ///     Gets all bulk checks the user has access to
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityBulkStatusesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.Unauthorized)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/bulk-check")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> GetAllBulkChecks()
    {
        try
        {
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var result = await _getAllBulkChecksUseCase.Execute(localAuthorityIds);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse 
            { 
                Errors = [new Error { Title = ex.Message }] 
            });
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
    ///     Loads results of bulk loads given a group Id
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityBulkResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.Unauthorized)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/bulk-check/{guid}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> BulkUploadResults(string guid)
    {
        try
        {
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var result = await _getBulkUploadResultsUseCase.Execute(guid, localAuthorityIds);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse 
            { 
                Errors = [new Error { Title = ex.Message }] 
            });
        }
        catch (NotFoundException)
        {
            return NotFound(new ErrorResponse
                { Errors = [new Error { Title = guid, Status = StatusCodes.Status404NotFound }] });
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
    ///     Soft deletes bulk check given a group Id
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(CheckEligibilityBulkDeleteResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpDelete("/bulk-check/{guid}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> DeleteBulkUpload(string guid)
    {
        try
        {
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var result = await _deleteBulkUploadUseCase.Execute(guid, localAuthorityIds);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (InvalidScopeException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
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


