using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class LocalAuthoritiesController : ControllerBase
{
    private readonly ILocalAuthority _localAuthority;

    public LocalAuthoritiesController(ILocalAuthority localAuthority)
    {
        _localAuthority = localAuthority;
    }

    [ProducesResponseType(typeof(LocalAuthoritySettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/local-authorities/{laCode:int}/settings")]
    public async Task<ActionResult> GetSettings(int laCode)
    {
        var la = await _localAuthority.GetLocalAuthority(laCode);

        if (la == null)
        {
            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Title = $"Local authority '{laCode}' not found" }]
            });
        }

        return Ok(new LocalAuthoritySettingsResponse
        {
            SchoolCanReviewEvidence = la.SchoolCanReviewEvidence
        });
    }
}

public class LocalAuthoritySettingsResponse
{
    public bool SchoolCanReviewEvidence { get; set; }
}
