using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
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
public class NotificationController : BaseController
{
    private readonly ILogger<NotificationController> _logger;
    private readonly ISendNotificationUseCase _sendNotificationUseCase;

    public NotificationController(ILogger<NotificationController> logger,
        ISendNotificationUseCase sendNotificationUseCase,
        IAudit audit)
        : base(audit)
    {
        _logger = logger;
        _sendNotificationUseCase = sendNotificationUseCase;
    }

    [ProducesResponseType(typeof(NotificationItemResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/notification")]
    [Authorize(Policy = PolicyNames.RequireNotificationScope)]
    public async Task<ActionResult> Notification([FromBody] NotificationRequest model)
    {
        try
        {
            var response = await _sendNotificationUseCase.Execute(model);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponse
                { Errors = [new Error { Title = "" }] });
        }
    }
}