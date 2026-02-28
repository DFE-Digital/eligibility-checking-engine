using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Extensions;
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
        var la = await _localAuthority.GetLocalAuthorityById(laCode);

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

    [ProducesResponseType(typeof(LocalAuthoritySettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/local-authorities/{laCode:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireLaOrAdminScope)]
    public async Task<ActionResult> UpdateSettings(
    int laCode,
    [FromBody] LocalAuthoritySettingsUpdateRequest request)
    {
        // In addition to the policy, enforce "affected LA only" unless Admin
        var isAdmin = User.HasScope("admin");

        if (!isAdmin)
        {
            var laIdFromToken = User.GetSingleScopeId("local_authority");

            // null => invalid scope state (multiple LAs or mixed), 0 => general LA access (all)
            if (laIdFromToken is null || laIdFromToken == 0 || laIdFromToken != laCode)
                return Unauthorized();
        }

        var updated = await _localAuthority.UpdateSchoolCanReviewEvidence(laCode, request.SchoolCanReviewEvidence);

        if (updated == null)
        {
            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Title = $"Local authority '{laCode}' not found" }]
            });
        }

        return Ok(new LocalAuthoritySettingsResponse
        {
            SchoolCanReviewEvidence = updated.SchoolCanReviewEvidence
        });
    }
}
