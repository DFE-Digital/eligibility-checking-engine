using CheckYourEligibility.API.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CheckYourEligibility.API.Extensions.Authorization;

public class RuleRequirementHandler : AuthorizationHandler<RuleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RuleRequirement requirement)
    {
        if (requirement.Rule.Evaluate(context.User))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}