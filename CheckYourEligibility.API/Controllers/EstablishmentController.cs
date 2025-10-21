using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class EstablishmentController : BaseController
{
    private readonly ILogger<EstablishmentController> _logger;
    private readonly ISearchEstablishmentsUseCase _searchUseCase;

    public EstablishmentController(
        ILogger<EstablishmentController> logger,
        ISearchEstablishmentsUseCase searchUseCase,
        IAudit audit)
        : base(audit)
    {
        _logger = logger;
        _searchUseCase = searchUseCase;
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
}