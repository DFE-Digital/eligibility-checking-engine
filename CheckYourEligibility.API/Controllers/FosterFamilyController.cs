using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class FosterFamilyController : BaseController
{
    private readonly ILogger<FosterFamilyController> _logger;
    private readonly string _localAuthorityScopeName;

    public FosterFamilyController(
        ILogger<FosterFamilyController> logger,
        IConfiguration configuration,
        IAudit audit
    ) : base(audit)
    {
        _logger = logger;
        _localAuthorityScopeName = _localAuthorityScopeName = configuration.GetValue<string>("Jwt:Scopes:local_authority") ?? "local_authority";
    }

    

}