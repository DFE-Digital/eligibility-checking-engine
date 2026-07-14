using System.Net;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Extensions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserController : BaseController
{
    private readonly ICreateOrUpdateUserUseCase _createOrUpdateUserUseCase;
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger, ICreateOrUpdateUserUseCase createOrUpdateUserUseCase,
        IAudit audit)
        : base(audit)
    {
        _logger = logger;
        _createOrUpdateUserUseCase = createOrUpdateUserUseCase;
    }

    /// <summary>
    ///     creates or returns existing user Id
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(UserSaveItemResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/user")]
    [Authorize(Policy = PolicyNames.RequireUserScope)]
    public async Task<ActionResult> User([FromBody] UserCreateRequest model)
    {
        if (model == null || model.Data == null)
            return BadRequest(new ErrorResponse
                { Errors = [new Error { Title = "Invalid request, data is required." }] });

        var response = await _createOrUpdateUserUseCase.Execute(model);
        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }
}