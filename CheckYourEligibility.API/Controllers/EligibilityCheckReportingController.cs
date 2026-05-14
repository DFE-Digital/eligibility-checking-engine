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
public class EligibilityCheckReportingController : BaseController
{
    private readonly ILogger<EligibilityCheckReportingController> _logger;
    private readonly string _localAuthorityScopeName;
    private readonly IGetEligibilityReportHistoryUseCase _getEligibilityReportHistoryUseCase;
    private readonly IGetEligibilityCheckReportingUseCase _getEligibilityCheckReportingUseCase;
    private readonly IDeleteEligibilityCheckReportUseCase _deleteEligibilityCheckReportUseCase;

    public EligibilityCheckReportingController(
        ILogger<EligibilityCheckReportingController> logger,
        IConfiguration configuration, 
        IAudit audit,
        IGetEligibilityReportHistoryUseCase getEligibilityReportHistoryUseCase,
        IGetEligibilityCheckReportingUseCase getEligibilityCheckReportingUseCase,
        IDeleteEligibilityCheckReportUseCase deleteEligibilityCheckReportUseCase
    ) : base(audit)
    {
        _logger = logger;
        _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
        _getEligibilityReportHistoryUseCase = getEligibilityReportHistoryUseCase;
        _getEligibilityCheckReportingUseCase = getEligibilityCheckReportingUseCase;
        _deleteEligibilityCheckReportUseCase = deleteEligibilityCheckReportUseCase;
    }

    /// <summary>
    ///     Return report status and id
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(EligibilityCheckReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/check-eligibility/report")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> EligibilityCheckReportRequest([FromBody] EligibilityCheckReportRequest model)
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

            var result = await _getEligibilityCheckReportingUseCase.Execute(model);

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
    ///     Gets all report history for a given LA
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(EligibilityCheckReportHistoryResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/check-eligibility/report-history/{localAuthorityId}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetAllReportHistory(string localAuthorityId, [FromQuery] int pageNumber)
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

            var result = await _getEligibilityReportHistoryUseCase.Execute(localAuthorityId, localAuthorityIds, pageNumber);

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
    ///     Soft deletes a report by setting its IsDeleted flag.
    /// </summary>
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpDelete("/check-eligibility/report-history/{reportId}")]
    [Authorize(Policy = PolicyNames.RequireBulkCheckScope)]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> DeleteReportHistory(Guid reportId)
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

            await _deleteEligibilityCheckReportUseCase.Execute(reportId, localAuthorityIds);

            return new StatusCodeResult(StatusCodes.Status204NoContent);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
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
    }
}
