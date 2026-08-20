using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Extensions.Authorization;

public static class RuleBuilder
{
    public static IAuthorizationRule UserType(UserType userType) => new UserTypeRule(userType);

    public static IAuthorizationRule OrganisationType(OrganisationType organisationType) => new OrganisationTypeRule(organisationType);
    
    public static IAuthorizationRule Role(UserRoleName role) => new RoleRule(role);

    public static IAuthorizationRule And(params IAuthorizationRule[] rules) => new AndRule(rules);

    public static IAuthorizationRule Or(params IAuthorizationRule[] rules) => new OrRule(rules);
}