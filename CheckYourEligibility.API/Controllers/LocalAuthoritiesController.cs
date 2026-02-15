using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class LocalAuthoritiesController : BaseController
{
    private readonly ILocalAuthority _localAuthority;

    public LocalAuthoritiesController(ILocalAuthority localAuthority, IAudit audit) : base(audit)
    {
        _localAuthority = localAuthority;
    }

    [ProducesResponseType(typeof(LocalAuthoritySettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/local-authorities/{laCode:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
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
