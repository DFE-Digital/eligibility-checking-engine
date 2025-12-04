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

    [HttpPost("/efe/api/v1/eligibility-events")]
    public IActionResult EligibilityEvents()
    {
        return Ok();
    }
}
