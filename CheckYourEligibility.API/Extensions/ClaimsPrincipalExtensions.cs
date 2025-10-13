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
    public static string UserOrgType(this ClaimsPrincipal user)
    {
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

    /// <summary>
    /// Checks if the user has either general scope OR exactly one specific scope ID, but not both.
    /// Returns false if user has multiple specific scope IDs or if both general and specific scopes are present.
    /// </summary>
    public static bool HasSingleScope(this ClaimsPrincipal user, string scopeName)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope").ToList();
        var hasGeneralScope = false;
        var specificIds = new List<int>();
        
        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Check for general scope
            if (scopes.Contains(scopeName))
                hasGeneralScope = true;
                
            // Collect specific IDs
            foreach (var scope in scopes)
            {
                if (scope.StartsWith($"{scopeName}:"))
                {
                    var idPart = scope.Substring($"{scopeName}:".Length);
                    if (int.TryParse(idPart, out var id))
                        specificIds.Add(id);
                }
            }
        }
        
        // Valid scenarios:
        // 1. Has general scope AND no specific IDs
        // 2. Has exactly one specific ID AND no general scope
        // Invalid scenarios:
        // - Both general and specific scopes present
        // - Multiple specific IDs
        // - No scopes at all
        
        if (hasGeneralScope && specificIds.Count > 0)
            return false; // Reject if both general and specific scopes are present
            
        if (hasGeneralScope && specificIds.Count == 0)
            return true; // General scope only
            
        return specificIds.Count == 1; // Exactly one specific ID only
    }

    /// <summary>
    /// Gets the single scope ID if user has exactly one, or 0 if user has general scope only.
    /// Returns null if user has multiple specific IDs, both general and specific scopes, or no valid scope.
    /// </summary>
    public static int? GetSingleScopeId(this ClaimsPrincipal user, string scopeName)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope").ToList();
        var hasGeneralScope = false;
        var specificIds = new List<int>();
        
        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Check for general scope
            if (scopes.Contains(scopeName))
                hasGeneralScope = true;
                
            // Collect specific IDs
            foreach (var scope in scopes)
            {
                if (scope.StartsWith($"{scopeName}:"))
                {
                    var idPart = scope.Substring($"{scopeName}:".Length);
                    if (int.TryParse(idPart, out var id))
                        specificIds.Add(id);
                }
            }
        }
        
        // Return null for invalid scenarios
        if (hasGeneralScope && specificIds.Count > 0)
            return null; // Both general and specific scopes present
            
        // Return 0 for general scope only
        if (hasGeneralScope && specificIds.Count == 0)
            return 0;
            
        // Return the single ID if exactly one exists
        if (specificIds.Count == 1)
            return specificIds[0];
            
        // Return null for other invalid scenarios (no scope or multiple IDs)
        return null;
    }
}