using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
public class EligibilityEventsController : BaseController
{
    private readonly ILogger<EligibilityEventsController> _logger;

    public EligibilityEventsController(ILogger<EligibilityEventsController> logger, IAudit audit) : base(audit)
    {
        _logger = logger;
    }

    [HttpPut("/efe/api/v1/eligibility-events/{id}")]
    public IActionResult EligibilityEvents(string id)
    {
        return Ok();
    }

    [HttpDelete("/efe/api/v1/eligibility-events/{id}")]
    public IActionResult DeleteEligibilityEvent(string id)
    {
        return Ok();
    }
}
