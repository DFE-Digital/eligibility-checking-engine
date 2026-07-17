using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using CheckYourEligibility.API.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

/// <summary>
/// TEMPORARY diagnostic endpoints for exercising DWP CAPI directly from within the deployed
/// app's own network path/credentials - added to evaluate DWP doc section 8.1
/// (GET /v2/citizens/{guid}), which the eligibility check flow does not otherwise call.
///
/// - Admin scope only (<see cref="PolicyNames.RequireAdminScope"/>).
/// - Hidden from Swagger/OpenAPI (<see cref="ApiExplorerSettingsAttribute.IgnoreApi"/>).
/// - GET only against /v2/citizens/{guid} - deliberately does not implement PATCH, since that
///   can write real citizen data at DWP.
///
/// Remove this controller once the 8.1 evaluation is complete.
/// </summary>
[ApiController]
[Route("admin/dwp-diagnostics")]
[Authorize(Policy = PolicyNames.RequireAdminScope)]
[ApiExplorerSettings(IgnoreApi = true)]
public class DwpDiagnosticsController : BaseController
{
    private readonly IDwpAdapter _dwpAdapter;
    private readonly ILogger<DwpDiagnosticsController> _logger;

    public DwpDiagnosticsController(
        IDwpAdapter dwpAdapter,
        ILogger<DwpDiagnosticsController> logger,
        IAudit audit) : base(audit)
    {
        _dwpAdapter = dwpAdapter;
        _logger = logger;
    }

    /// <summary>
    /// Calls DWP CAPI POST /v2/citizens/match with the supplied details, using the same request
    /// shape as a real check. Returns the raw response, including the resolved citizen guid on a
    /// successful match.
    /// </summary>
    [HttpPost("citizen-match")]
    public async Task<ActionResult> CitizenMatch([FromBody] DwpDiagnosticsMatchRequest request)
    {
        if (string.IsNullOrEmpty(request.Nino) || request.Nino.Length < 5)
            return BadRequest("Nino must be at least 5 characters.");

        var correlationId = Guid.NewGuid().ToString();
        var ninoFragment = request.Nino.Substring(request.Nino.Length - 5, 4);

        var matchRequest = new CitizenMatchRequest
        {
            Jsonapi = new CitizenMatchRequest.CitizenMatchRequest_Jsonapi { Version = "1.0" },
            Data = new CitizenMatchRequest.CitizenMatchRequest_Data
            {
                Type = "Match",
                Attributes = new CitizenMatchRequest.CitizenMatchRequest_Attributes
                {
                    LastName = request.LastName,
                    DateOfBirth = request.DateOfBirth,
                    NinoFragment = ninoFragment
                }
            }
        };

        _logger.LogWarning(
            "DWP diagnostics: citizen-match invoked via admin endpoint, correlationId:{CorrelationId}",
            correlationId);

        var result = await _dwpAdapter.GetCitizen(matchRequest, request.Type, correlationId);

        return new OkObjectResult(new
        {
            correlationId,
            result.Guid,
            result.ResponseCode,
            result.CAPIResponseCode,
            result.ResponseBody
        });
    }

    /// <summary>
    /// Calls DWP CAPI GET /v2/citizens/{guid} directly (doc section 8.1) and returns the raw
    /// response.
    /// </summary>
    [HttpGet("citizen/{guid}")]
    public async Task<ActionResult> GetCitizen(string guid,
        [FromQuery] CheckYourEligibility.API.Domain.Enums.CheckEligibilityType type = CheckYourEligibility.API.Domain.Enums.CheckEligibilityType.FreeSchoolMeals)
    {
        var correlationId = Guid.NewGuid().ToString();

        _logger.LogWarning(
            "DWP diagnostics: GET citizen/{Guid} invoked via admin endpoint, correlationId:{CorrelationId}",
            guid, correlationId);

        var result = await _dwpAdapter.GetCitizenByGuid(guid, correlationId, type);

        return new OkObjectResult(new
        {
            correlationId,
            result.ResponseCode,
            result.CAPIResponseCode,
            result.ResponseBody
        });
    }
}
