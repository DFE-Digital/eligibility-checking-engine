using System.Security.Claims;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Extensions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CheckYourEligibility.API.Domain.Authorization;

public abstract class AuthorizationRuleBase : IAuthorizationRule
{
    public IAuthorizationRequirement Build()
    {
        return new RuleRequirement(this);
    }

    public abstract bool Evaluate(ClaimsPrincipal user);
}

public class UserTypeRule(UserType userType) : AuthorizationRuleBase
{
    private readonly string _userType = userType.ToString();

    public override bool Evaluate(ClaimsPrincipal user)
    {
        return user.HasClaim("UserType", _userType);
    }
}

public class RoleRule(UserRoleName role) : AuthorizationRuleBase
{
    private readonly string _role = role.ToString();

    public override bool Evaluate(ClaimsPrincipal user)
    {
        return user.IsInRole(_role);
    }
}

public class OrganisationTypeRule(OrganisationType organisationType) : AuthorizationRuleBase
{
    private readonly string _organisationType = organisationType.ToString();

    public override bool Evaluate(ClaimsPrincipal user)
    {
        return user.HasClaim("OrganisationType", _organisationType);
    }
}

public class AndRule(params IAuthorizationRule[] rules) : AuthorizationRuleBase
{
    private readonly IReadOnlyCollection<IAuthorizationRule> _rules = rules;

    public override bool Evaluate(ClaimsPrincipal user)
    {
        return _rules.All(r => r.Evaluate(user));
    }
}

public class OrRule(params IAuthorizationRule[] rules) : AuthorizationRuleBase
{
    private readonly IReadOnlyCollection<IAuthorizationRule> _rules = rules;

    public override bool Evaluate(ClaimsPrincipal user)
    {
        return _rules.Any(r => r.Evaluate(user));
    }
}