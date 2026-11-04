using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CheckYourEligibility.Core.Domain.Authorization;

public interface IAuthorizationRule
{
    IAuthorizationRequirement Build();
    bool Evaluate(ClaimsPrincipal user);
}