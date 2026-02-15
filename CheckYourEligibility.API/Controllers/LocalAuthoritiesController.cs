using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

/// <summary>
///     Local Authorities Controller
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class LocalAuthoritiesController : ControllerBase
{
    private readonly ILocalAuthorityGateway _localAuthorityGateway;

    public LocalAuthoritiesController(ILocalAuthorityGateway localAuthorityGateway)
    {
        _localAuthorityGateway = localAuthorityGateway;
    }

    /// <summary>
    ///     Returns settings for a given Local Authority (by LA code / LocalAuthorityID).
    /// </summary>
    /// <param name="laCode">Local authority code (maps to LocalAuthorityID)</param>
    [ProducesResponseType(typeof(LocalAuthoritySettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/local-authorities/{laCode:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)] // <— remove if schools must call this directly
    public async Task<ActionResult> GetSettings(int laCode)
    {
        var la = await _localAuthorityGateway.GetLocalAuthorityById(laCode);

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

/// <summary>
///     Local authority settings response
/// </summary>
public class LocalAuthoritySettingsResponse
{
    public bool SchoolCanReviewEvidence { get; set; }
}
