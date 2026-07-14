using System.Net;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Extensions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class MultiAcademyTrustsController : BaseController
{
    private readonly IMultiAcademyTrust _multiAcademyTrust;
    private readonly IGetEstablishmentsByMultiAcademyTrustIdUseCase _getEstablismentsByMultiAcademyTrustId;
    private readonly string _multiAcademyTrustScopeName;
    private readonly string _adminScopeName;

    public MultiAcademyTrustsController(
    IMultiAcademyTrust multiAcademyTrust,
    IGetEstablishmentsByMultiAcademyTrustIdUseCase getEstablishmentsByMultiAcademyTrustIdUseCase,
    IAudit audit,
    IConfiguration configuration) : base(audit)
    {
        _multiAcademyTrust = multiAcademyTrust;
        _getEstablismentsByMultiAcademyTrustId = getEstablishmentsByMultiAcademyTrustIdUseCase;
        _multiAcademyTrustScopeName = configuration.GetValue<string>("Jwt:Scopes:multi_academy_trust") ?? "multi_academy_trust";
        _adminScopeName = configuration.GetValue<string>("Jwt:Scopes:admin") ?? "admin";
    }

    [ProducesResponseType(typeof(MultiAcademyTrustSettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/multi-academy-trusts/{multiAcademyTrustId:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetSettings(int multiAcademyTrustId)
    {
        var mat = await _multiAcademyTrust.GetMultiAcademyTrustById(multiAcademyTrustId);

        if (mat == null)
        {
            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Title = $"Multi Academy Trust '{multiAcademyTrustId}' not found" }]
            });
        }

        return Ok(new MultiAcademyTrustSettingsResponse
        {
            AcademyCanReviewEvidence = mat.AcademyCanReviewEvidence
        });
    }

    [ProducesResponseType(typeof(MultiAcademyTrustSettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPatch("/multi-academy-trusts/{multiAcademyTrustId:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireMatOrAdminScope)]
    public async Task<ActionResult> UpdateSettings(
        int multiAcademyTrustId,
        [FromBody] MultiAcademyTrustSettingsUpdateRequest request)
    {
        var isAdmin = User.HasScope(_adminScopeName);

        if (!isAdmin)
        {
            var matIdFromToken = User.GetSingleScopeId(_multiAcademyTrustScopeName);

            if (matIdFromToken is null || matIdFromToken == 0 || matIdFromToken != multiAcademyTrustId)
                return Forbid();
        }

        var updated = await _multiAcademyTrust.UpdateAcademyCanReviewEvidence(
            multiAcademyTrustId,
            request.AcademyCanReviewEvidence);

        if (updated == null)
        {
            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Title = $"Multi Academy Trust '{multiAcademyTrustId}' not found" }]
            });
        }

        return Ok(new MultiAcademyTrustSettingsResponse
        {
            AcademyCanReviewEvidence = updated.AcademyCanReviewEvidence
        });
    }

    [ProducesResponseType(typeof(EstablishmentResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/multi-academy-trusts/{multiAcademyTrustId:int}/establishments")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetEstablishmentsByMultiAcademyTrustId(int multiAcademyTrustId)
    {
        try
        {
            var establishments = await _getEstablismentsByMultiAcademyTrustId.Execute(multiAcademyTrustId);

            return new OkObjectResult(establishments) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {

            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Status = StatusCodes.Status404NotFound, 
                Title = $"Multi academy trust '{multiAcademyTrustId}' not found.", 
                Detail = $"{ex}" }]
            });

        }

    }
}