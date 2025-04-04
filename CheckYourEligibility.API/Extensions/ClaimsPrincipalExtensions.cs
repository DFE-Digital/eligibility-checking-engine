using System.Security.Claims;

namespace CheckYourEligibility.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetLocalAuthorityId(this ClaimsPrincipal user, string localAuthorityScopeName)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope").ToList();

        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Check for local_authority:XXX format
            var specificScope = scopes.FirstOrDefault(s => s.StartsWith($"{localAuthorityScopeName}:"));
            if (specificScope != null) return specificScope.Substring($"{localAuthorityScopeName}:".Length);

            // Check for local_authority scope
            if (scopes.Contains(localAuthorityScopeName)) return "all";
        }

        return null;
    }

    public static bool HasScopeWithColon(this ClaimsPrincipal user, string scopeValue)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope");

        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (scopes.Any(s => s == scopeValue || s.StartsWith($"{scopeValue}:"))) return true;
        }

        return false;
    }

    // Helper method to check for a specific scope value
    public static bool HasScope(this ClaimsPrincipal user, string scopeValue)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope");

        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (scopes.Contains(scopeValue)) return true;
        }

        return false;
    }
}