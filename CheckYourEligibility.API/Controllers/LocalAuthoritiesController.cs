using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class LocalAuthoritiesController : BaseController
{
    private readonly ILocalAuthority _localAuthority;
    private readonly IEligibilityPolicy _eligibilityPolicy;
    private const string AdminScope = "admin";
    private const string LocalAuthorityScope = "local_authority";

    public LocalAuthoritiesController(ILocalAuthority localAuthority, IAudit audit, IEligibilityPolicy eligibility) : base(audit)
    {
        _localAuthority = localAuthority;
        _eligibilityPolicy = eligibility;
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

        // extract the eligibility polciies for the LA
        // to return into the response
        List<EligibilityPolicyResponse> eligiblity = new();
        int[] laPolicies = [la.FreeSchoolMealsPolicyID, la.EarlyYearsPupilPremiumPolicyID, la.TwoYearPolicyID];

        foreach (var policyId in laPolicies)
        {

            var policy = await _eligibilityPolicy.GeEligibilityPolicyByIdAsync(policyId);
            EligibilityPolicyResponse eligibilityPolicy = new()
            {
                CheckType = policy.CheckType.ToString(),
                EligibilityCriteria = policy.EligibilityCriteria.ToString()
            };

            eligiblity.Add(eligibilityPolicy);
        }


        return Ok(new LocalAuthoritySettingsResponse
        {
            SchoolCanReviewEvidence = la.SchoolCanReviewEvidence,
            EligibilityPolicies = eligiblity

        });
    }

    [ProducesResponseType(typeof(LocalAuthoritySettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/local-authorities/{laCode:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireLaOrAdminScope)]
    public async Task<ActionResult> UpdateSettings(
    int laCode,
    [FromBody] LocalAuthoritySettingsUpdateRequest request)
    {
        // In addition to the policy, enforce "affected LA only" unless Admin
        var isAdmin = User.HasScope(AdminScope);

        if (!isAdmin)
        {
            var laIdFromToken = User.GetSingleScopeId(LocalAuthorityScope);

            // null => invalid scope state (multiple LAs or mixed), 0 => general LA access (all)
            if (laIdFromToken is null || laIdFromToken == 0 || laIdFromToken != laCode)
                return Forbid();
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
