using System.Net;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.Constants.ErrorMessages;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Extensions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class FosterFamilyController : BaseController
{
    private readonly ILogger<FosterFamilyController> _logger;
    private readonly string _localAuthorityScopeName;

    private readonly IGetFosterFamilyUseCase _getFosterFamily;
    private readonly ICreateFosterFamilyUseCase _createFosterFamily;
    private readonly IUpdateFosterCarerUseCase _updateFosterCarer;
    private readonly IDeleteFosterCarerUseCase _deleteFosterCarer;
    private readonly IDeleteFosterPartnerUseCase _deleteFosterPartner;
    private readonly ISearchFosterFamiliesUseCase _searchFosterFamilies;

    private readonly IGetFosterChildUseCase _getFosterChild;
    private readonly ICreateFosterChildUseCase _createFosterChild;
    private readonly IUpdateFosterChildUseCase _updateFosterChild;
    private readonly IDeleteFosterChildUseCase _deleteFosterChild;

    public FosterFamilyController(
        ILogger<FosterFamilyController> logger,
        IConfiguration configuration,
        IGetFosterFamilyUseCase getFosterFamily,
        ICreateFosterFamilyUseCase createFosterFamily,
        IUpdateFosterCarerUseCase updateFosterCarer,
        IDeleteFosterCarerUseCase deleteFosterCarer,
        IDeleteFosterPartnerUseCase deleteFosterPartner,
        ISearchFosterFamiliesUseCase searchFosterFamilies,
        IGetFosterChildUseCase getFosterChild,
        ICreateFosterChildUseCase createFosterChild,
        IUpdateFosterChildUseCase updateFosterChild,
        IDeleteFosterChildUseCase deleteFosterChild,
        IAudit audit
    ) : base(audit)
    {
        _logger = logger;
        _localAuthorityScopeName = _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";

        _getFosterFamily = getFosterFamily;
        _createFosterFamily = createFosterFamily;
        _updateFosterCarer = updateFosterCarer;
        _deleteFosterCarer = deleteFosterCarer;
        _deleteFosterPartner = deleteFosterPartner;
        _searchFosterFamilies = searchFosterFamilies;

        _getFosterChild = getFosterChild;
        _createFosterChild = createFosterChild;
        _updateFosterChild = updateFosterChild;
        _deleteFosterChild = deleteFosterChild;
    }

    [ProducesResponseType(typeof(FosterFamilyResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/foster-family/{fosterCarerId}")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetFosterFamily(Guid fosterCarerId, bool includeChildren = false)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            var result = await _getFosterFamily.Execute(fosterCarerId, localAuthorityId.Value, includeChildren);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "Foster family not found", Detail = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting foster family");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType(typeof(FosterFamilyCreatedResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/foster-family")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CreateFosterFamily([FromBody] FosterFamilyRequest model)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            var response = await _createFosterFamily.Execute(model, localAuthorityId.Value);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating foster family");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpPatch("/foster-family/{fosterCarerId}")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> UpdateFosterCarer(Guid fosterCarerId, [FromBody] UpdateFosterCarerRequest model)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            await _updateFosterCarer.Execute(fosterCarerId, localAuthorityId.Value, model);
            return new StatusCodeResult(StatusCodes.Status204NoContent);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating foster carer");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpDelete("/foster-family/{fosterCarerId}")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> DeleteFosterCarer(Guid fosterCarerId)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            await _deleteFosterCarer.Execute(fosterCarerId, localAuthorityId.Value);
            return new StatusCodeResult(StatusCodes.Status204NoContent);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting foster carer");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpDelete("/foster-family/{fosterCarerId}/partner")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> DeleteFosterPartner(Guid fosterCarerId)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            await _deleteFosterPartner.Execute(fosterCarerId, localAuthorityId.Value);
            return new StatusCodeResult(StatusCodes.Status204NoContent);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting foster partner");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType(typeof(FosterFamiliesSearchResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [HttpGet("/foster-family/search")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> SearchFosterFamilies(
    [FromQuery] int pageNumber, [FromQuery] int pageSize)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            var request = new FosterFamiliesSearchRequest
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var response = await _searchFosterFamilies.Execute(
                request,
                localAuthorityId.Value);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching foster families");

            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType(typeof(FosterChildResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/foster-family/child/{fosterChildId}")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetFosterChild(Guid fosterChildId, bool includeFosterCarer = false)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            var result = await _getFosterChild.Execute(fosterChildId, localAuthorityId.Value, includeFosterCarer);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "Foster child not found", Detail = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting foster child");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType(typeof(FosterChildCreatedResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/foster-family/{fosterCarerId}/child")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CreateFosterChild(Guid fosterCarerId, [FromBody] FosterChildRequest model)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            var response = await _createFosterChild.Execute(model, localAuthorityId.Value, fosterCarerId, DateTime.UtcNow);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "Foster carer not found", Detail = ex.Message }] });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating foster child");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType(typeof(FosterChildResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/foster-family/child/{fosterChildId}")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> UpdateFosterChild(Guid fosterChildId, [FromBody] UpdateFosterChildRequest model)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            var response = await _updateFosterChild.Execute(fosterChildId, localAuthorityId.Value, model);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "Foster child not found", Detail = ex.Message }] });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating foster child");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpDelete("/foster-family/child/{fosterChildId}")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> DeleteFosterChild(Guid fosterChildId)
    {
        try
        {
            int? localAuthorityId = User.GetSingleScopeId(_localAuthorityScopeName);
            if (localAuthorityId is null or < 0)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = FosterFamilyValidationMessages.NoLocalAuthorityScopeFound }] });
            }

            await _deleteFosterChild.Execute(fosterChildId, localAuthorityId.Value);
            return new StatusCodeResult(StatusCodes.Status204NoContent);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = "Foster child not found", Detail = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting foster child");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

}