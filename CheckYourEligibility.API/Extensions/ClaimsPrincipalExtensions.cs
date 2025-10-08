using System.Security.Claims;

namespace CheckYourEligibility.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    private static readonly string _localAuthorityScope = "local_authority";
    private static readonly string _multiAcademyTrust = "multi_academy_trust";
    private static readonly string _establishment = "establishment";
    /// <summary>
    /// Gets all specific scope id s from the user's claims.
    /// Returns a list of ids for 'local_authority:xx' scopes, or a list with 0 if 'local_authority' (all) is present.
    /// </summary>
    public static List<int> GetSpecificScopeIds(this ClaimsPrincipal user, string scopeName)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope").ToList();
        var ids = new List<int>();
        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // If 'all', return [0] immediately
            if (scopes.Contains(scopeName))
                return new List<int> { 0 };
            // Add all valid local_authority:xx
            foreach (var s in scopes)
            {
                if (s.StartsWith($"{scopeName}:"))
                {
                    var idPart = s.Substring($"{scopeName}:".Length);
                    if (int.TryParse(idPart, out var id))
                        ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Checks if the user has a scope with a colon (e.g., 'scope:xx').
    /// </summary>
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

    /// <summary>
    /// Checks if the user has a specific scope value.
    /// </summary>
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
    /// <summary>
    /// Check user organisation which will be passed in the scope.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static string UserOrgType(this ClaimsPrincipal user) {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope").ToList();    
        string scopes = scopeClaims[0].Value;
        if (scopes.Contains(_localAuthorityScope))
        {
             return _localAuthorityScope;
        }
        else if (scopes.Contains(_multiAcademyTrust))
        {
             return _multiAcademyTrust;
        }
        else
        {
             return _establishment;
        }      
    }
}