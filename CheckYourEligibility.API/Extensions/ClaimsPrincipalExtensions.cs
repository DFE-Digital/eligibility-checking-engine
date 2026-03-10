using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Security.Claims;

namespace CheckYourEligibility.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    private static readonly string _localAuthority = "local_authority";
    private static readonly string _multiAcademyTrust = "multi_academy_trust";
    private static readonly string _establishment = "establishment";
    private static readonly string _admin = "admin";

    /// <summary>
    /// gets username from clientid - if no ':' found then userName = null
    /// else it returns the found userName that is passed from the portals
    /// if username = null AND clientId is not from the portals then userName = clientID
    /// Check if client_id is of portal- return appropriate source
    /// </summary>
    /// <param name="user"></param>
    /// <returns>source of check and username</returns>
    private static (string, string?) GetCheckSourceAndUserNameFromClientId(this ClaimsPrincipal user)
    {

        string source = string.Empty;
        string clientId = user.Claims.FirstOrDefault(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value;
        bool scopehasAdmin = user.HasScope(_admin);

        string? userName = user.PortalUserName(clientId);

        if (clientId.Contains("childcare-admin"))
        {
            source = CheckSource.childcare_admin_portal;
        }
        else if (clientId.Contains("free-school-meals-frontend"))
        {
            source = CheckSource.fsm_parent_portal;
        }

        else if (clientId.Contains("free-school-meals-admin"))
        {

            source = CheckSource.fsm_admin_portal;
        }
        else if (clientId.Contains("eligibility-checking-engine-support"))
        {
            source = CheckSource.support_portal;
        }
        else if (scopehasAdmin)
        {
            source = CheckSource.api_admin;
            userName = clientId;
        }
        else if (!scopehasAdmin)
        {

            source = CheckSource.api_enduser;
            userName = clientId;
        }
        return (source, userName);

    }
    /// <summary>
    /// Check passed orgs ID
    /// and map OrgID and Type
    /// if multiple Ids found orgId = 0 and Orgtype = ambiguous
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static CheckMetaData CalculateMetaData(this ClaimsPrincipal user)
    {

        var sourceAndUserName = user.GetCheckSourceAndUserNameFromClientId();
        CheckMetaData meta = new();
        meta.OrganisationID = 0;
        meta.OrganisationType = OrganisationType.ambiguous;
        meta.Source = sourceAndUserName.Item1;
        meta.UserName = sourceAndUserName.Item2;

        bool hasLaScope = user.HasScope(_localAuthority);
        bool hasMatScope = user.HasScope(_multiAcademyTrust);
        bool hasEstScope = user.HasScope(_establishment);

        // If orgs scopes not found return orgId = 0 OrgType = unspecified
        if (!hasLaScope && !hasMatScope && !hasEstScope)
        {

            meta.OrganisationType = OrganisationType.unspecified;
            return meta;
        }

        // If more than one Id is found per scope or no scope is found, returns null 
        // If only generic scope is passed , returns 0
        // Else it returns the ID.
        int? matId = user.GetSingleScopeId(_multiAcademyTrust);
        int? laId = user.GetSingleScopeId(_localAuthority);
        int? establishmentId = user.GetSingleScopeId(_establishment);

        bool hasMultipleLAId = false;

        // Check if at least one of the org scopes have more than one ID  passed
        if ((laId == null && hasLaScope) || (matId == null && hasMatScope)  || (establishmentId == null && hasEstScope))
        {
            return meta;

        }
        // If different Orgs IDs detected then orgID = 0 and orgType = ambiguous
        else if (((establishmentId != null && establishmentId > 0 ) && (laId != null && laId > 0)) || 
                ((establishmentId != null && establishmentId > 0) && (matId != null && matId > 0)) || 
                ((laId != null && establishmentId > 0) && (matId != null && matId > 0))) { return meta; }
        else if (laId > 0) { meta.OrganisationID = laId; meta.OrganisationType = OrganisationType.local_authority; }
        else if (matId > 0) { meta.OrganisationID = matId; meta.OrganisationType = OrganisationType.multi_academy_trust; }
        else if (establishmentId > 0) { meta.OrganisationID = establishmentId; meta.OrganisationType = OrganisationType.establishment; }
        else { meta.OrganisationType = OrganisationType.unspecified; }

        return meta;

    }


    /// <summary>
    /// Gets all specific scope ids from the user's claims.
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


    private static string? PortalUserName(this ClaimsPrincipal user, string clientId)
    {
        string? userName = null;

        // if no colon found - return then userName = null;
        if (!clientId.Contains(":")) return userName;
        // else substring everything after colon
        int startIndex = clientId.IndexOf(":");
        userName = clientId.Substring(startIndex + 1, clientId.Length);
        return userName;

    }

    /// <summary>
    /// Checks if the user has a scope with a colon (e.g., 'scope:xx').
    /// </summary>
    ///   
    public static bool HasScopeWithColon(this ClaimsPrincipal user, string scopeValue)
    {
        var scopeClaims = user.Claims.Where(c => c.Type == "scope");

        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (scopes.Any(s => s.StartsWith($"{scopeValue}:"))) return true;
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