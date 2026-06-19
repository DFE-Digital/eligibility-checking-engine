using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
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
    private readonly ILocalAuthoritiesUseCase _localAuthorityUseCase;
    private readonly IGetEstablishmentsByLocalAuthorityIdUseCase _getEstablismentsByLocalAuthorityId;
    private readonly ILocalAuthority _localAuthority;
    private const string AdminScope = "admin";
    private const string LocalAuthorityScope = "local_authority";

    public LocalAuthoritiesController(ILocalAuthoritiesUseCase localAuthorityUseCase, IAudit audit, ILocalAuthority localAuthority, IGetEstablishmentsByLocalAuthorityIdUseCase getEstablishmentsByLocalAuthorityIdUseCase) : base(audit)
    {
        _localAuthorityUseCase = localAuthorityUseCase;
        _localAuthority = localAuthority;
        _getEstablismentsByLocalAuthorityId = getEstablishmentsByLocalAuthorityIdUseCase;
    }

    [ProducesResponseType(typeof(LocalAuthoritySettingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/local-authorities/{laCode:int}/settings")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetSettings(int laCode)
    {
        try
        {

            var laSettings = await _localAuthorityUseCase.Execute(laCode);

            return new OkObjectResult(laSettings);
        }
        catch (NotFoundException ex)
        {

            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Status = StatusCodes.Status404NotFound, Title = $"Local authority '{laCode}' not found" }]
            });

        }

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

    [ProducesResponseType(typeof(EstablishmentResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/local-authorities/{laCode:int}/establishments")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetEstablishmentsByLocalAuthorityId(int laCode)
    {
        try
        {
            var establishments = await _getEstablismentsByLocalAuthorityId.Execute(laCode);

            return new OkObjectResult(establishments) { StatusCode = StatusCodes.Status200OK };
        }
        catch (NotFoundException ex)
        {

            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Status = StatusCodes.Status404NotFound, 
                Title = $"Local authority '{laCode}' not found.", 
                Detail = $"{ex}" }]
            });

        }

    }

}
