using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class FosterFamilyController : BaseController
{
    private readonly ILogger<FosterFamilyController> _logger;
    private readonly ICreateFosterFamilyUseCase _createFosterFamilyUseCase;
    private readonly IGetFosterFamilyUseCase _getFosterFamilyUseCase;
    private readonly IUpdateFosterFamilyUseCase _updateFosterFamilyUseCase;
    private readonly string _localAuthorityScopeName;

    public FosterFamilyController(
        ILogger<FosterFamilyController> logger,
        ICreateFosterFamilyUseCase createFosterFamilyUseCase,
        IGetFosterFamilyUseCase getFosterFamilyUseCase,
        IUpdateFosterFamilyUseCase updateFosterFamilyUseCase,
        IAudit audit,
        IConfiguration configuration
    ) : base(audit)
    {
        _logger = logger;
        _createFosterFamilyUseCase = createFosterFamilyUseCase;
        _getFosterFamilyUseCase = getFosterFamilyUseCase;
        _updateFosterFamilyUseCase = updateFosterFamilyUseCase;
        _localAuthorityScopeName = _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
        
    }

    /// <summary>
    /// Posts a foster family 
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(FosterFamilySaveItemResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/create/foster-families")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> FosterFamily([FromBody] FosterFamilyRequest model)
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

            var response = await _createFosterFamilyUseCase.Execute(model, localAuthorityIds);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating foster family");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Gets an foster family by guid
    /// </summary>
    /// <param name="guid"></param>
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/foster-family/{guid}")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> FosterFamily(string guid)
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

            var response = await _getFosterFamilyUseCase.Execute(guid);

            if (response == null) return NotFound(new ErrorResponse { Errors = [new Error { Title = "Not Found", Detail = $"foster family with guid {guid} not found" }] });

            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error finding foster family for guid {guid?.Replace(Environment.NewLine, "")}");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Updates an Foster family application
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ApplicationUpdateResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/foster-family/{guid}")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> UpdateFosterFamily(string guid, [FromBody] FosterFamilyUpdateRequest model)
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

            var response = await _updateFosterFamilyUseCase.Execute(guid, model);
            if (response == null) return NotFound(new ErrorResponse { Errors = [new Error { Title = "" }] });
            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error updating foster family application for guid {guid?.Replace(Environment.NewLine, "")}");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

}