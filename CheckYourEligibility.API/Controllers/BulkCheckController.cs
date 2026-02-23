using System.Net;
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
using NotFoundException = CheckYourEligibility.API.Domain.Exceptions.NotFoundException;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class BulkCheckController : BaseController
{
    private readonly int _bulkUploadRecordCountLimit;
    private readonly ICheckEligibilityBulkUseCase _checkEligibilityBulkUseCase;
    private readonly IGetBulkCheckStatusesUseCase _getBulkCheckStatusesUseCase;
    private readonly IGetBulkUploadProgressUseCase _getBulkUploadProgressUseCase;
    private readonly IGetBulkUploadResultsUseCase _getBulkUploadResultsUseCase;
    private readonly IDeleteBulkCheckUseCase _deleteBulkUploadUseCase;
    private readonly IGetAllBulkChecksUseCase _getAllBulkChecksUseCase;
    private readonly ILogger<BulkCheckController> _logger;
    private readonly string _localAuthorityScopeName;

    public BulkCheckController(
        ILogger<BulkCheckController> logger,
        IAudit audit,
        IConfiguration configuration,
        ICheckEligibilityBulkUseCase checkEligibilityBulkUseCase,
        IGetBulkCheckStatusesUseCase getBulkCheckStatusesUseCase,
        IGetBulkUploadProgressUseCase getBulkUploadProgressUseCase,
        IGetBulkUploadResultsUseCase getBulkUploadResultsUseCase,
        IDeleteBulkCheckUseCase deleteBulkUploadUseCase,
        IGetAllBulkChecksUseCase getAllBulkChecksUseCase
    )
        : base(audit)
    {
        _logger = logger;
        _bulkUploadRecordCountLimit = configuration.GetValue<int>("BulkEligibilityCheckLimit");
        _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";

        _checkEligibilityBulkUseCase = checkEligibilityBulkUseCase;
        _getBulkCheckStatusesUseCase = getBulkCheckStatusesUseCase;
        _getBulkUploadProgressUseCase = getBulkUploadProgressUseCase;
        _getBulkUploadResultsUseCase = getBulkUploadResultsUseCase;
        _deleteBulkUploadUseCase = deleteBulkUploadUseCase;
        _getAllBulkChecksUseCase = getAllBulkChecksUseCase;
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
            if(model.Meta==null) model.Meta = new CheckEligibilityRequestBulkBase(); 
            if (!model.Meta.LocalAuthorityId.HasValue && localAuthorityIds.Count == 1 && localAuthorityIds[0] != 0)
            {
                model.Meta.LocalAuthorityId = localAuthorityIds[0];
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
    /// PolicyNames.RequireLaOrMatOrSchoolScope ensures at least one org with id is found in scope, unless it is local_authority
    /// <returns></returns>
    /// <remarks>
    /// The type of eligibility check is determined by the endpoint path (/bulk-check/free-school-meals).
    /// You do not need to include a 'type' field in the request body.
    /// </remarks>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/free-school-meals")]
    [SwaggerRequestExample(typeof(CheckEligibilityRequestBulk), typeof(CheckFSMBulkModelExample))]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)] 
    public async Task<ActionResult> CheckEligibilityBulkFsm([FromBody] CheckEligibilityRequestBulk model)
    {
        try
        {
            // Extract local authority IDs from user claims
            var localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            //   var matId = User.GetSingleScopeId(_multiAcademyTrustScopeName);
            // var schoolId = User.GetSingleScopeId(_establishmentScopeName);

            // If schoolId or matId is not null it means there is an id as the policy enforces an ID if either of the scopes is provided
            // we will check school first as it is the lowest form of org
            // NOTE: Bulk check column in DB will change from LocalAuthorityID to OrganisationId and Organisaion Type in next piece of work
            // to accommodate orgs better.
            //if (schoolId != null)
            //{
            //  // placeholder
            //  //  model.OrganisationId = schoolId;
            //  // model.OrganisationType = OrganisationType.Establishment
            //}
            //else if (matId != null)
            //{    // placeholder
            //    //  model.OrganisationId = schoolId;
            //    //  model.OrganisationType = OrganisationType.Establishment
            //}

            // NOTE: To not disturb current business rules around local authoriy we also allow generic local_authoriy scope to be passed here
            // If no school or mat scope found then we record the Id of the local_authority if one is passed
            // else do not pass anything as this was the logic previously.
            if(model.Meta==null) model.Meta = new CheckEligibilityRequestBulkBase(); 
            if (!model.Meta.LocalAuthorityId.HasValue && localAuthorityId != null && localAuthorityId != 0)
            {
                model.Meta.LocalAuthorityId = localAuthorityId;
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
    /// <remarks>
    /// The type of eligibility check is determined by the endpoint path (/bulk-check/two-year-offer).
    /// You do not need to include a 'type' field in the request body.
    /// </remarks>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/two-year-offer")]
    [SwaggerRequestExample(typeof(CheckEligibilityRequestBulk), typeof(Check2YOBulkModelExample))]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CheckEligibilityBulk2yo([FromBody] CheckEligibilityRequestBulk model)
    {
        try
        {
            // Check which org and extract local authority IDs from user claims
            // If result is 0 that means that only general scope is used.
            var localAuthorityIds = User.GetSpecificScopeIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }
            // 
            // Set LocalAuthorityId if not provided and if only one id is found.
            // lili: (there should never be a case where more the one id is found according to the business rules in auth use case )
            if(model.Meta==null) model.Meta = new CheckEligibilityRequestBulkBase(); 
            if (!model.Meta.LocalAuthorityId.HasValue && localAuthorityIds.Count == 1 && localAuthorityIds[0] != 0)
            {
                model.Meta.LocalAuthorityId = localAuthorityIds[0];
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
    /// <remarks>
    /// The type of eligibility check is determined by the endpoint path (/bulk-check/early-year-pupil-premium).
    /// You do not need to include a 'type' field in the request body.
    /// </remarks>
    [ProducesResponseType(typeof(CheckEligibilityResponseBulk), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/bulk-check/early-year-pupil-premium")]
    [SwaggerRequestExample(typeof(CheckEligibilityRequestBulk), typeof(CheckEYPPBulkModelExample))]
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
            if(model.Meta==null) model.Meta = new CheckEligibilityRequestBulkBase(); 
            if (!model.Meta.LocalAuthorityId.HasValue && localAuthorityIds.Count == 1 && localAuthorityIds[0] != 0)
            {
                model.Meta.LocalAuthorityId = localAuthorityIds[0];
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
    [HttpGet("/bulk-check/search")]
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
}