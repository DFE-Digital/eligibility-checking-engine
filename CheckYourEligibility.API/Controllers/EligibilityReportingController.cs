using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EligibilityReportingController : BaseController
{
    private readonly ICreateEligibilityCheckReportUseCase _createEligibilityCheckReportUseCase;
    private readonly IGetEligibilityReportUseCase _getEligibilityCheckReportUseCase;
    private readonly IGetEligibilityReportHistoryUseCase _getEligibilityReportHistoryUseCase;
    private readonly ILogger<EligibilityReportingController> _logger;
    private readonly string _localAuthorityScopeName;
    public EligibilityReportingController(
        ILogger<EligibilityReportingController> logger,
        IAudit audit,
        IConfiguration configuration,
        ICreateEligibilityCheckReportUseCase createEligibilityCheckReportUseCase,
        IGetEligibilityReportUseCase getEligibilityCheckReportUseCase,
        IGetEligibilityReportHistoryUseCase getEligibilityReportHistoryUseCase
    ) : base(audit)
    {
        _logger = logger;
        _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
        _createEligibilityCheckReportUseCase = createEligibilityCheckReportUseCase;
        _getEligibilityCheckReportUseCase = getEligibilityCheckReportUseCase;
        _getEligibilityReportHistoryUseCase = getEligibilityReportHistoryUseCase;
    }

    /// <summary>
    ///     Returns reports of asscioated checks between a set time period
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(EligibilityCheckReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/eligibility-check/report")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> CreateEligibilityCheckReport([FromBody] EligibilityCheckReportRequest model)
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


            if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(model.LocalAuthorityID.Value))
            {
                return Unauthorized(new ErrorResponse
                {
                    Errors = [new Error { Title = "You do not have permission to generate a report for this Local Authority" }]
                });
            }

            var result = await _createEligibilityCheckReportUseCase.Execute(model);

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
            { Errors = [new Error { Status = StatusCodes.Status404NotFound }] });
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
    ///     Gets report with checks for a given report id, with pagination
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(EligibilityCheckReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.Unauthorized)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/eligibility-check/report/{reportId}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    public async Task<ActionResult> GetEligibilityCheckReport(Guid reportId, [FromQuery] string localAuthorityId, [FromQuery] int pageNumber = 1)
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

            var result = await _getEligibilityCheckReportUseCase.Execute(reportId, localAuthorityId, localAuthorityIds, pageNumber);

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
    ///     Gets all report history for a given LA
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(EligibilityCheckReportHistoryResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/check-eligibility/report-history/{localAuthorityId}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetReportsGeneratedHistory(string localAuthorityId)
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

            var result = await _getEligibilityReportHistoryUseCase.Execute(localAuthorityId, localAuthorityIds);

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
}