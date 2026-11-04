using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Tests.Controllers;

[ExcludeFromCodeCoverage]
public abstract class ControllerTestBase : TestBase
{

    internal string TestClaimIdentifier { get; set; } = "unit-test";

    internal List<Claim> SetupSpecificScopeIdClaims(List<int> ids, string scopeName)
    {
        var claims = new List<Claim>();

        // Add appropriate scope claims based on ids
        if (ids.Contains(0))
        {
            claims.Add(new Claim("scope", scopeName));
        }
        else
        {
            var scopeValue = string.Join(" ", ids.Select(id => $"{scopeName}:{id}"));
            claims.Add(new Claim("scope", scopeValue));
        }

        return claims;
    }

    internal void SetupControllerWithLocalAuthorityIds(ControllerBase controller, List<int> localAuthorityIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = SetupSpecificScopeIdClaims(localAuthorityIds, "local_authority");
        claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", TestClaimIdentifier));

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }
    
    internal void SetupControllerWithLaAndMatIds(ControllerBase controller, List<int> localAuthorityIds, List<int> multiAcademyTrustIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = SetupSpecificScopeIdClaims(localAuthorityIds, "local_authority");
        claims.AddRange(SetupSpecificScopeIdClaims(multiAcademyTrustIds, "multi_academy_trust"));
        claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", TestClaimIdentifier));

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }
}