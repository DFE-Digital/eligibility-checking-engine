using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValidationException = FluentValidation.ValidationException;
using CheckYourEligibility.API.Extensions;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ApplicationController : BaseController
{
    private readonly ICreateApplicationUseCase _createApplicationUseCase;
    private readonly IGetApplicationUseCase _getApplicationUseCase;
    private readonly string _localAuthorityScopeName;
    private readonly ILogger<ApplicationController> _logger;
    private readonly ISearchApplicationsUseCase _searchApplicationsUseCase;
    private readonly IUpdateApplicationStatusUseCase _updateApplicationStatusUseCase;

    public ApplicationController(
        ILogger<ApplicationController> logger,
        IConfiguration configuration,
        ICreateApplicationUseCase createApplicationUseCase,
        IGetApplicationUseCase getApplicationUseCase,
        ISearchApplicationsUseCase searchApplicationsUseCase,
        IUpdateApplicationStatusUseCase updateApplicationStatusUseCase,
        IAudit audit)
        : base(audit)
    {
        _logger = logger;
        _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
        _createApplicationUseCase = createApplicationUseCase;
        _getApplicationUseCase = getApplicationUseCase;
        _searchApplicationsUseCase = searchApplicationsUseCase;
        _updateApplicationStatusUseCase = updateApplicationStatusUseCase;
    }

    /// <summary>
    ///     Posts an application for FreeSchoolMeals
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ApplicationSaveItemResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/application")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    public async Task<ActionResult> Application([FromBody] ApplicationRequest model)
    {
        try
        {
            var response = await _createApplicationUseCase.Execute(model);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Gets an application by guid
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ApplicationItemResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/application/{guid}")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    public async Task<ActionResult> Application(string guid)
    {
        try
        {
            var response = await _getApplicationUseCase.Execute(guid);

            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }
    }

    /// <summary>
    ///     Searches for applications based on the supplied filter
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ApplicationSearchResponse), (int)HttpStatusCode.OK)]
    [Consumes("application/json", "application/vnd.api+json; version=1.0")]
    [HttpPost("/application/search")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> ApplicationSearch([FromBody] ApplicationRequestSearch model)
    {
        var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
        if (localAuthorityIds == null || localAuthorityIds.Count == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = "No local authority scope found" }]
            });
        }

        int? requestedLocalAuthority = model.Data?.LocalAuthority;
        // If not 'all', must match one of the allowed LocalAuthorities
        if (!localAuthorityIds.Contains(0) && (requestedLocalAuthority == null || !localAuthorityIds.Contains(requestedLocalAuthority.Value)))
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = "Local authority scope does not match requested LocalAuthority" }]
            });
        }

        var response = await _searchApplicationsUseCase.Execute(model);
        return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
    }

    /// <summary>
    ///     Updates the status of an application
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ApplicationStatusUpdateResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/application/{guid}")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    public async Task<ActionResult> ApplicationStatusUpdate(string guid,
        [FromBody] ApplicationStatusUpdateRequest model)
    {
        var response = await _updateApplicationStatusUseCase.Execute(guid, model);
        if (response == null) return NotFound(new ErrorResponse { Errors = [new Error { Title = "" }] });
        return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
    }
}