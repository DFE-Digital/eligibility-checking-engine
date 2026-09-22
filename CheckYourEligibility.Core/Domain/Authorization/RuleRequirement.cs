using Microsoft.AspNetCore.Authorization;

namespace CheckYourEligibility.Core.Domain.Authorization;

public class RuleRequirement : IAuthorizationRequirement
{
    public IAuthorizationRule Rule { get; }

    public RuleRequirement(IAuthorizationRule rule)
    {
        Rule = rule;
    }
}