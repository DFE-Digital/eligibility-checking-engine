using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class EstablishmentController : BaseController
{
    private readonly ILogger<EstablishmentController> _logger;
    private readonly ISearchEstablishmentsUseCase _searchUseCase;
    private readonly ISupportSearchEstablishmentUseCase _supportSearchUseCase;
    private readonly ISupportGetEstablishmentUseCase _supportGetEstablishmentUseCase;
    private readonly IApplication _applicationGateway;

    public EstablishmentController(
    ILogger<EstablishmentController> logger,
    ISearchEstablishmentsUseCase searchUseCase,
    ISupportSearchEstablishmentUseCase supportSearchUseCase,
    ISupportGetEstablishmentUseCase supportGetEstablishmentUseCase,
    IAudit audit,
    IApplication applicationGateway)
    : base(audit)
    {
        _logger = logger;
        _searchUseCase = searchUseCase;
        _supportGetEstablishmentUseCase = supportGetEstablishmentUseCase;
        _applicationGateway = applicationGateway;
    }

    [ProducesResponseType(typeof(IEnumerable<Establishment>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/establishment/search")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> Search(string query, string? la, string? mat)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            return BadRequest(new ErrorResponse
            { Errors = [new Error { Title = "At least 3 characters are required to query." }] });

        var results = await _searchUseCase.Execute(query, la, mat);

        return new ObjectResult(new EstablishmentSearchResponse { Data = results })
        { StatusCode = StatusCodes.Status200OK };
    }

    /// <summary>
    /// Gets the multi academy trust ID for a given establishment
    /// </summary>
    /// <param name="establishmentId">Establishment ID</param>
    /// <returns>Multi academy trust ID, or 0 if the establishment is not part of a MAT</returns>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/establishment/{establishmentId}/multi-academy-trust-id")]
    [Authorize(Policy = PolicyNames.RequireLaOrMatOrSchoolScope)]
    public async Task<ActionResult> GetMultiAcademyTrustIdForEstablishment(int establishmentId)
    {
        if (establishmentId <= 0)
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = "A valid establishmentId is required." }]
            });
        }

        var matId = await _applicationGateway.GetMultiAcademyTrustIdForEstablishment(establishmentId);

        return new ObjectResult(matId)
        {
            StatusCode = StatusCodes.Status200OK
        };
    }

    [ProducesResponseType(typeof(SupportEstablishmentSearchResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpGet("/establishment/support-search")]
    [Authorize(Policy = PolicyNames.RequireEstablishmentManagement)]
    public async Task<ActionResult> Support_Search(
     SupportEstablishmentSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) &&
            !request.Urn.HasValue)
        {
            return BadRequest(new ErrorResponse
            {
                Errors =
                [
                    new Error
                {
                    Title = "Either Name or URN must be supplied."
                }
                ]
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Name) &&
            request.Name.Length < 3)
        {
            return BadRequest(new ErrorResponse
            {
                Errors =
                [
                    new Error
                {
                    Title = "At least 3 characters are required to query."
                }
                ]
            });
        }

        var results = await _supportSearchUseCase.Execute(request);

        return Ok(results);
    }
    [ProducesResponseType(typeof(SupportEstablishmentResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpGet("/establishment/{establishmentId}")]
    [Authorize(Policy = PolicyNames.RequireEstablishmentManagement)]
    public async Task<ActionResult> GetEstablishment(int establishmentId)
    {
        var result = await _supportGetEstablishmentUseCase.Execute(establishmentId);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}