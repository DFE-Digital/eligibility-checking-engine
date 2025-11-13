using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.Api.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for authenticating a user.
/// </summary>
public interface IAuthenticateUserUseCase
{
    /// <summary>
    ///     Prepares the JWT configuration and authenticates the user.
    /// </summary>
    /// <param name="credentials">Client credentials</param>
    /// <returns>JWT auth response with token</returns>
    Task<JwtAuthResponse> Execute(SystemUser credentials);
}

/// <summary>
///     Use case for authenticating a user using OAuth2 standards.
/// </summary>
public class AuthenticateUserUseCase : IAuthenticateUserUseCase
{
    private readonly IAudit _auditGateway;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthenticateUserUseCase> _logger;

    /// <summary>
    ///     Constructor for the AuthenticateUserUseCase.
    /// </summary>
    /// <param name="auditGateway">Audit gateway for logging authentication attempts</param>
    /// <param name="logger">Logger service</param>
    /// <param name="jwtSettings">JWT settings configuration</param>
    public AuthenticateUserUseCase(IAudit auditGateway, ILogger<AuthenticateUserUseCase> logger,
        JwtSettings jwtSettings)
    {
        _auditGateway = auditGateway;
        _logger = logger;
        _jwtSettings = jwtSettings;
    }

    /// <summary>
    ///     Prepares the JWT configuration and authenticates the user.
    /// </summary>
    /// <param name="credentials">Client credentials</param>
    /// <returns>JWT auth response with token</returns>
    /// <exception cref="AuthenticationException">Thrown when authentication fails</exception>
    public async Task<JwtAuthResponse> Execute(SystemUser credentials)
    {
        if (credentials.grant_type != null && credentials.grant_type != "client_credentials")
            _logger.LogWarning($"Unsupported grant_type: {credentials.grant_type}".Replace(Environment.NewLine, ""));

        if (credentials.client_id.IsNullOrEmpty())
        {
            _logger.LogError($"Invalid client identifier: {credentials.client_id.Replace(Environment.NewLine, "")}");
            throw new InvalidClientException("Invalid client identifier");
        }

        // Get client secret from configuration
        string? secret = null;
        if (_jwtSettings.Clients.TryGetValue(credentials.safe_client_id(), out var value)) secret = value?.Secret;
        if (secret == null)
        {
            _logger.LogError(
                $"Authentication secret not found for identifier: {credentials.client_id.Replace(Environment.NewLine, "")}");
            throw new InvalidClientException("The client authentication failed");
        }

        // Get and validate allowed scopes from client configuration
        var clientSettings = _jwtSettings.Clients[credentials.safe_client_id()];
        string? allowedScopes = clientSettings?.Scope;

        if (string.IsNullOrEmpty(allowedScopes))
        {
            _logger.LogError(
                $"Allowed scopes not found for client: {credentials.client_id.Replace(Environment.NewLine, "")}");
            throw new InvalidScopeException("Client is not authorized for any scopes");
        }

        var jwtConfig = new JwtConfig
        {
            Key = _jwtSettings.Key,
            Issuer = _jwtSettings.Issuer,
            ExpectedSecret = secret,
            AllowedScopes = allowedScopes
        };

        return await ExecuteAuthentication(credentials, jwtConfig);
    }

    /// <summary>
    ///     Execute the authentication process.
    /// </summary>
    private async Task<JwtAuthResponse> ExecuteAuthentication(SystemUser credentials, JwtConfig jwtConfig)
    {
        await _auditGateway.CreateAuditEntry(AuditType.Client, credentials.client_id);

        if (!ValidateSecret(credentials.client_secret, jwtConfig.ExpectedSecret)) throw new InvalidClientException();

        if (!ValidateScopes(credentials.scope, jwtConfig.AllowedScopes)) throw new InvalidScopeException();

        var tokenString = GenerateJSONWebToken(credentials.client_id, credentials.scope, jwtConfig, out var expires);
        var expiresInSeconds = (int)(expires - DateTime.UtcNow).TotalSeconds;

        if (string.IsNullOrEmpty(tokenString)) throw new ServerErrorException();


        return new JwtAuthResponse
            { expires_in = expiresInSeconds, access_token = tokenString, token_type = "Bearer" };
    }

    private static bool ValidateSecret(string secret, string expectedSecret)
    {
        return !string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(expectedSecret) && secret == expectedSecret;
    }

    private static bool ValidateScopes(string? requestedScopes, string? allowedScopes)
    {
        // Validate that we have allowed scopes configured (server-side configuration)
        if (string.IsNullOrEmpty(allowedScopes)) return false;

        // Normalize user input - treat null/empty/default as empty scope request
        var normalizedRequestedScopes = string.IsNullOrEmpty(requestedScopes) || requestedScopes == "default" 
            ? string.Empty 
            : requestedScopes;

        // Parse allowed scopes (server-controlled)
        var allowedScopesList = allowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // For empty scope requests, they are valid if we have server configuration
        // (The fact that allowedScopes is not null/empty means the server is properly configured)
        if (string.IsNullOrEmpty(normalizedRequestedScopes))
        {
            return true; // Empty scope request is valid for properly configured clients
        }

        // Parse and validate requested scopes
        var requestedScopesList = normalizedRequestedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Validate local_authority scope business rule: either general OR exactly one specific ID
        if (!ValidateLocalAuthorityScopeRule(requestedScopesList)) return false;

        return requestedScopesList.All(requestedScope => IsScopeValid(requestedScope, allowedScopesList));
    }

    /// <summary>
    /// Validates the local authority scope business rule: users can have either 
    /// general "local_authority" scope OR exactly one specific local authority ID,
    /// but not both and not multiple specific IDs.
    /// </summary>
    /// <param name="requestedScopesList">Array of requested scopes</param>
    /// <returns>True if the local authority scope rule is satisfied, false otherwise</returns>
    private static bool ValidateLocalAuthorityScopeRule(string[] requestedScopesList)
    {
        // Only validate local authority scope rule if local authority scopes are present
        var hasLocalAuthorityScopes = requestedScopesList.Any(scope => 
            scope == "local_authority" || scope.StartsWith("local_authority:"));
            
        if (!hasLocalAuthorityScopes)
            return true; // No local authority scopes present, rule doesn't apply

        // Validate and sanitize scopes before creating ClaimsPrincipal
        var sanitizedScopes = ValidateAndSanitizeScopes(requestedScopesList);
        if (sanitizedScopes == null)
            return false; // Invalid scopes detected

        // Now safely create ClaimsPrincipal with validated data and reuse existing logic
        var claims = sanitizedScopes.Select(scope => new Claim("scope", scope));
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        // Use our existing validation logic for consistency
        return principal.HasSingleScope("local_authority");
    }

    /// <summary>
    /// Validates and sanitizes scope strings to ensure they are safe for processing.
    /// </summary>
    /// <param name="scopes">Array of scope strings to validate</param>
    /// <returns>List of sanitized scopes, or null if any scope is invalid</returns>
    private static List<string>? ValidateAndSanitizeScopes(string[] scopes)
    {
        if (scopes == null || scopes.Length == 0)
            return new List<string>();

        var sanitizedScopes = new List<string>();
        
        foreach (var scope in scopes)
        {
            if (string.IsNullOrWhiteSpace(scope))
                continue;
                
            var trimmedScope = scope.Trim();
            
            // Validate scope format for security
            if (!IsValidScopeFormat(trimmedScope))
                return null; // Invalid scope detected
                
            sanitizedScopes.Add(trimmedScope);
        }

        return sanitizedScopes;
    }

    /// <summary>
    /// Validates that the scope string contains only allowed characters and follows expected format.
    /// </summary>
    /// <param name="scope">The scope string to validate</param>
    /// <returns>True if the scope format is valid, false otherwise</returns>
    private static bool IsValidScopeFormat(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return false;

        // Allow only alphanumeric characters, underscores, and colons
        // This prevents injection of special characters that could affect processing
        var allowedChars = scope.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ':');
        if (!allowedChars)
            return false;

        // Additional validation for specific scope patterns
        if (scope.Contains(':'))
        {
            var parts = scope.Split(':');
            if (parts.Length != 2)
                return false;

            var scopeType = parts[0];
            var scopeId = parts[1];

            // Validate known scope types
            var validScopeTypes = new[] { "local_authority", "multi_academy_trust", "establishment" };
            if (!validScopeTypes.Contains(scopeType))
                return false;

            // Validate that ID is numeric
            if (!int.TryParse(scopeId, out var id) || id <= 0)
                return false;
        }

        return true;
    }

    private static bool IsScopeValid(string requestedScope, string[] allowedScopesList)
    {
        if (allowedScopesList.Contains(requestedScope)) return true;

        // local_authority:XX pattern
        if (requestedScope.StartsWith("local_authority:") || requestedScope.StartsWith("multi_academy_trust:") || requestedScope.StartsWith("establishment:"))
            return IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // If we got here, the scope is not valid
        return false;
    }

    private static bool IsSpecificScopeIdValid(string requestedScope, string[] allowedScopesList)
    {
        // check if there's a match with specific local_authority:xx pattern in allowed scopes
        var requestedScopeType = requestedScope.Split(':', 2)[0];
        var requestedAuthorityId = requestedScope.Split(':', 2)[1];

        // Additional check that the scope type is one that supports specific IDs
        List<string> allowedSpecificScopeTypes = ["local_authority", "multi_academy_trust", "establishment"];
        if (!allowedSpecificScopeTypes.Contains(requestedScopeType)) return false;

        // If a client has "local_authority" scope, they should have access to any "local_authority:XX" specific scope
        if (allowedScopesList.Contains(requestedScopeType)) return true;

        foreach (var allowedScope in allowedScopesList)
            if (allowedScope.StartsWith($"{requestedScopeType}:"))
            {
                var allowedAuthority = allowedScope.Split(':', 2)[1];

                if (requestedAuthorityId == allowedAuthority) return true;
            }

        return false;
    }

    private static string GenerateJSONWebToken(string identifier, string? scope, JwtConfig jwtConfig,
        out DateTime expires)
    {
        try
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.Key));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claimsList = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, identifier),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Only include scope claim if scope was provided
            if (!string.IsNullOrEmpty(scope) && scope != "default") claimsList.Add(new Claim("scope", scope));

            expires = DateTime.UtcNow.AddMinutes(120);
            var token = new JwtSecurityToken(
                jwtConfig.Issuer,
                jwtConfig.Issuer,
                claimsList,
                expires: expires,
                signingCredentials: signingCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception)
        {
            expires = DateTime.MinValue;
            return string.Empty; // Return empty string instead of null to avoid null reference exception
        }
    }
}