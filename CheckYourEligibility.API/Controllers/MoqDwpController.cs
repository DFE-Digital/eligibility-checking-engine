// LOCAL DEVELOPMENT ONLY
// This controller simulates the DWP Citizen API (CAPI) so the full eligibility check
// flow works locally without needing access to the real DWP API.
//
// appsettings.Development.json routes Dwp:ApiHost → https://localhost:7117/MoqDWP,
// so DwpAdapter calls land here instead of the real DWP service.
//
// Test outcome is controlled by the parent's lastName:
//   lastName starts with 'Z' → parentNotFound   (citizen not matched at all)
//   lastName starts with 'N' → notEligible       (citizen matched, no qualifying benefit)
//   anyone else              → eligible           (UC live award, takeHomePay well below threshold)

using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.DWP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("MoqDWP")]
[AllowAnonymous]
[ExcludeFromCodeCoverage(Justification = "Local development stub — not part of production code paths")]
public class MoqDwpController : ControllerBase
{
    private const string NotEligibleSentinelGuid = "NOT_ELIGIBLE";

    /// <summary>
    /// Fake OAuth2 token endpoint. DwpAdapter calls this before every citizen lookup.
    /// Returns a dummy bearer token valid for 1 hour.
    /// </summary>
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult GetToken([FromForm] string? client_id, [FromForm] string? client_secret)
    {
        return Ok(new
        {
            access_token = "moq-dwp-local-dev-token",
            expires_in = 3600,
            token_type = "Bearer"
        });
    }

    /// <summary>
    /// Fake citizen match endpoint (POST /MoqDWP/v2/citizens/match).
    /// Outcome is driven by the parent's lastName:
    ///   'Z...' → 404 Not Found  (parentNotFound)
    ///   'N...' → 200 but returns a sentinel GUID that will produce notEligible in the claims call
    ///   else   → 200 citizen found, GUID = ninoFragment (for traceability in logs)
    /// </summary>
    [HttpPost("v2/citizens/match")]
    public IActionResult MatchCitizen([FromBody] CitizenMatchRequest request)
    {
        var lastName = request?.Data?.Attributes?.LastName ?? string.Empty;
        var nino = request?.Data?.Attributes?.NinoFragment ?? "UNKNOWN";

        // Simulate parent not found
        if (lastName.StartsWith("Z", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        // Eligible or notEligible — encode outcome in the GUID so the claims call can read it
        var guid = lastName.StartsWith("N", StringComparison.OrdinalIgnoreCase)
            ? NotEligibleSentinelGuid
            : nino;

        return Ok(new DwpMatchResponse
        {
            Jsonapi = new DwpMatchResponse.DwpResponse_Jsonapi { Version = "2.0" },
            Data = new DwpMatchResponse.DwpResponse_Data
            {
                Id = guid,
                Type = "Match",
                Attributes = new DwpMatchResponse.DwpResponse_Attributes { MatchingScenario = "ScenarioA" }
            }
        });
    }

    /// <summary>
    /// Fake benefit claims endpoint (GET /MoqDWP/v2/citizens/{guid}/claims).
    /// Returns 404 for the notEligible sentinel GUID, otherwise returns a live UC award
    /// with takeHomePay = 100 (well below the FSM threshold of ~616 → eligible).
    /// </summary>
    [HttpGet("v2/citizens/{guid}/claims")]
    public IActionResult GetCitizenClaims(string guid)
    {
        if (guid == NotEligibleSentinelGuid)
            return NotFound();

        var response = new DwpClaimsResponse
        {
            jsonapi = new Jsonapi { version = "2.0" },
            data = new List<Datum>
            {
                new Datum
                {
                    id = guid,
                    type = "Claim",
                    attributes = new Attributes
                    {
                        benefitType = "universal_credit",
                        startDate = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd"),
                        endDate = null,
                        awards = new List<Award>
                        {
                            new Award
                            {
                                amount = 100,
                                startDate = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd"),
                                endDate = DateTime.UtcNow.AddMonths(11).ToString("yyyy-MM-dd"),
                                status = "live",
                                awardComponents = new List<AwardComponent>(),
                                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100.00 }
                            }
                        }
                    }
                }
            },
            links = new Links { self = $"/v2/citizens/{guid}/claims" }
        };

        return Ok(response);
    }
}
