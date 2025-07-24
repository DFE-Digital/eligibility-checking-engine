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
    private readonly IImportApplicationsUseCase _importApplicationsUseCase;

    public ApplicationController(
        ILogger<ApplicationController> logger,
        IConfiguration configuration,
        ICreateApplicationUseCase createApplicationUseCase,
        IGetApplicationUseCase getApplicationUseCase,
        ISearchApplicationsUseCase searchApplicationsUseCase,
        IUpdateApplicationStatusUseCase updateApplicationStatusUseCase,
        IImportApplicationsUseCase importApplicationsUseCase,
        IAudit audit)
        : base(audit)
    {
        _logger = logger;
        _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
        _createApplicationUseCase = createApplicationUseCase;
        _getApplicationUseCase = getApplicationUseCase;
        _searchApplicationsUseCase = searchApplicationsUseCase;
        _updateApplicationStatusUseCase = updateApplicationStatusUseCase;
        _importApplicationsUseCase = importApplicationsUseCase;
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
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> Application([FromBody] ApplicationRequest model)
    {
        try
        {
            var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var response = await _createApplicationUseCase.Execute(model, localAuthorityIds);
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
            _logger.LogError(ex, "Error creating application");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Gets an application by guid
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>    [ProducesResponseType(typeof(ApplicationItemResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/application/{guid}")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> Application(string guid)
    {
        try
        {
            var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var response = await _getApplicationUseCase.Execute(guid, localAuthorityIds);

            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = guid }] });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
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
        try
        {
            var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var response = await _searchApplicationsUseCase.Execute(model, localAuthorityIds);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching applications");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
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
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> ApplicationStatusUpdate(string guid,
        [FromBody] ApplicationStatusUpdateRequest model)
    {
        try
        {
            var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var response = await _updateApplicationStatusUseCase.Execute(guid, model, localAuthorityIds);
            if (response == null) return NotFound(new ErrorResponse { Errors = [new Error { Title = "" }] });
            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error updating application status for guid {guid?.Replace(Environment.NewLine, "")}");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    /// Bulk imports applications from a CSV or JSON file
    /// </summary>
    /// <param name="request">The bulk import request containing the CSV or JSON file</param>
    /// <returns>Import results with success/failure counts and error details</returns>
    [ProducesResponseType(typeof(ApplicationBulkImportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("multipart/form-data")]
    [HttpPost("/application/bulk-import")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> BulkImportApplications([FromForm] ApplicationBulkImportRequest request)
    {
        try
        {
            var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var response = await _importApplicationsUseCase.Execute(request, localAuthorityIds);
            return Ok(response);
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
            _logger.LogError(ex, "Error during bulk import");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    /// Bulk imports applications from JSON body data
    /// </summary>
    /// <param name="request">The bulk import request containing application data in JSON format</param>
    /// <returns>Import results with success/failure counts and error details</returns>
    [ProducesResponseType(typeof(ApplicationBulkImportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json")]
    [HttpPost("/application/bulk-import-json")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> BulkImportApplicationsFromJson([FromBody] ApplicationBulkImportJsonRequest request)
    {
        try
        {
            var localAuthorityIds = User.GetLocalAuthorityIds(_localAuthorityScopeName);
            if (localAuthorityIds == null || localAuthorityIds.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Title = "No local authority scope found" }]
                });
            }

            var response = await _importApplicationsUseCase.ExecuteFromJson(request, localAuthorityIds);
            return Ok(response);
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
            _logger.LogError(ex, "Error during bulk import from JSON");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }
}