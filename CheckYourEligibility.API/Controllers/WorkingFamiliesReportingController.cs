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
public class WorkingFamiliesReportingController : BaseController
{
    private readonly ILogger<WorkingFamiliesReportingController> _logger;
    private readonly IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase _getAllWorkingFamiliesEventsByEligibilityCodeUseCase;
    private readonly string _localAuthorityScopeName;
    public WorkingFamiliesReportingController(
        ILogger<WorkingFamiliesReportingController> logger,
        IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase getAllWorkingFamiliesEventsByEligibilityCodeUseCase,
        IAudit audit,
        IConfiguration configuration
    ) : base(audit)
    {
        _logger = logger;
        _getAllWorkingFamiliesEventsByEligibilityCodeUseCase = getAllWorkingFamiliesEventsByEligibilityCodeUseCase;
        _localAuthorityScopeName = _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
    }

    /// <summary>
    /// Returns events by eligibility code
    /// </summary>
    /// <param name="eligibilityCode"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/working-families-reporting/{eligibilityCode}")]
    [Authorize(Policy = PolicyNames.RequireApplicationScope)]
    [Authorize(Policy = PolicyNames.RequireLocalAuthorityScope)]
    public async Task<ActionResult> GetAllWorkingFamiliesEventsByEligibilityCode(string eligibilityCode)
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

            var response = await _getAllWorkingFamiliesEventsByEligibilityCodeUseCase.Execute(eligibilityCode, localAuthorityIds);

            if (response == null)
                return NotFound(new ErrorResponse { Errors = [new Error { Title = "Not Found", Detail = $"Working family events with code {eligibilityCode} not found" }] });

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
                $"Error finding events for eligibility code {eligibilityCode?.Replace(Environment.NewLine, "")}");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }


}