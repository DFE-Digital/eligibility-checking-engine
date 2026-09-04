using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Exceptions;
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
    public WorkingFamiliesReportingController(
        ILogger<WorkingFamiliesReportingController> logger,
        IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase getAllWorkingFamiliesEventsByEligibilityCodeUseCase,
        IAudit audit
    ) : base(audit)
    {
        _logger = logger;
        _getAllWorkingFamiliesEventsByEligibilityCodeUseCase = getAllWorkingFamiliesEventsByEligibilityCodeUseCase;
    }

    /// <summary>
    /// Returns events by eligibility code
    /// </summary>
    /// <param name="eligibilityCode"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/working-families-reporting/{eligibilityCode}")]
    [Authorize(Policy = PolicyNames.GetEligibilityCodeHistory)]
    public async Task<ActionResult> GetAllWorkingFamiliesEventsByEligibilityCode(string eligibilityCode)
    {
        try
        {
            var response = await _getAllWorkingFamiliesEventsByEligibilityCodeUseCase.Execute(eligibilityCode);
            if (response == null)
            {
                return NotFound(new ErrorResponse { Errors = [new Error { Title = "Not Found", Detail = $"No working family events found for code {eligibilityCode}" }] });
            }
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
            _logger.LogError(ex, $"Error finding events for eligibility code {eligibilityCode}");
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }
}
