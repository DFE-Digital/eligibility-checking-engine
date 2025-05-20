using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.Api.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

// [ExcludeFromCodeCoverage]
[ApiController]
public class Oauth2Controller : Controller
{
    private readonly IAuthenticateUserUseCase _authenticateUserUseCase;
    private readonly ILogger<Oauth2Controller> _logger;

    public Oauth2Controller(ILogger<Oauth2Controller> logger, IAuthenticateUserUseCase authenticateUserUseCase)
    {
        _logger = logger;
        _authenticateUserUseCase = authenticateUserUseCase;
    }

    [AllowAnonymous]
    [ProducesResponseType(typeof(JwtAuthResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.Unauthorized)]
    [HttpPost("/oauth2/token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> LoginForm([FromForm] SystemUser credentials)
    {
        try
        {
            var response = await _authenticateUserUseCase.Execute(credentials);

            _logger.LogInformation($"{credentials.client_id?.Replace(Environment.NewLine, "")} authenticated");
            return Ok(response);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogWarning(
                $"{credentials.client_id?.Replace(Environment.NewLine, "")} authentication failed: {ex.ErrorCode}");
            return Unauthorized(new ErrorResponse
            {
                Errors =
                [
                    new Error
                    {
                        Title = ex.ErrorCode,
                        Detail = ex.ErrorDescription
                    }
                ]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Unexpected error authenticating {credentials.client_id?.Replace(Environment.NewLine, "")}");
            return Unauthorized(new ErrorResponse
            {
                Errors =
                [
                    new Error
                    {
                        Title = "server_error",
                        Detail = "The authorization server encountered an unexpected error"
                    }
                ]
            });
        }
    }
}