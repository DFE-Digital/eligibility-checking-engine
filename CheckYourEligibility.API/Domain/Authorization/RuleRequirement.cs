using Microsoft.AspNetCore.Authorization;

namespace CheckYourEligibility.API.Domain.Authorization;

public class RuleRequirement : IAuthorizationRequirement
{
    public IAuthorizationRule Rule { get; }

    public RuleRequirement(IAuthorizationRule rule)
    {
        Rule = rule;
    }
}