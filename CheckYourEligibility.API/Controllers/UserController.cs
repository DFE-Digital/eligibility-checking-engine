using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
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
    ///  Creates a new user or returns the identifier of an existing user.
    /// </summary>
    /// <param name="model">
    /// The user creation request.
    /// </param>
    /// <returns>
    /// A response containing the user identifier.
    /// </returns>
    [HttpPost("/user")]
    [Authorize(Policy = PolicyNames.RequireUserScope)]
    public async Task<ActionResult> User([FromBody] UserCreateRequest model)
    {
        try
        {
            if (model?.Data == null)
            {
                return BadRequest(new ErrorResponse { Errors = [new Error { Title = "Invalid request, data is required" }]});
            }

            model.metaData = HttpContext.User.CalculateMetaData();
            var response = await _createOrUpdateUserUseCase.Execute(model);

            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (UserSaveException ex)
        {
            _logger.LogWarning(ex, "Failed to create or update user");

            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = ex.Message }]
            });
        }
    }
}