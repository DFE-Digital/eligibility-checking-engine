using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.Api.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
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
    /// <param name="configuration">Application configuration</param>
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
    /// <param name="auditService">Audit service for logging authentication attempts</param>
    /// <param name="logger">Logger service</param>
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
    /// <param name="configuration">Application configuration</param>
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
        if (_jwtSettings.Clients.TryGetValue(credentials.client_id, out var value)) secret = value?.Secret;
        if (secret == null)
        {
            _logger.LogError(
                $"Authentication secret not found for identifier: {credentials.client_id.Replace(Environment.NewLine, "")}");
            throw new InvalidClientException("The client authentication failed");
        }

        var jwtConfig = new JwtConfig
        {
            Key = _jwtSettings.Key,
            Issuer = _jwtSettings.Issuer,
            ExpectedSecret = secret
        };

        // Get and validate allowed scopes
        if (!string.IsNullOrEmpty(credentials.client_id) && !string.IsNullOrEmpty(credentials.scope))
        {
            jwtConfig.AllowedScopes = _jwtSettings.Clients[credentials.client_id]?.Scope;
            if (string.IsNullOrEmpty(jwtConfig.AllowedScopes))
            {
                _logger.LogError(
                    $"Allowed scopes not found for client: {credentials.client_id.Replace(Environment.NewLine, "")}");
                throw new InvalidScopeException("Client is not authorized for any scopes");
            }
        }

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
            { Token = tokenString, expires_in = expiresInSeconds, access_token = tokenString, token_type = "Bearer" };
    }

    private static bool ValidateSecret(string secret, string expectedSecret)
    {
        return !string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(expectedSecret) && secret == expectedSecret;
    }

    private static bool ValidateScopes(string requestedScopes, string allowedScopes)
    {
        // Empty or default scopes are always valid
        if (string.IsNullOrEmpty(requestedScopes) || requestedScopes == "default") return true;

        // If we have specific scope requests but no allowed scopes, it's invalid
        if (string.IsNullOrEmpty(allowedScopes)) return false;

        var requestedScopesList = requestedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allowedScopesList = allowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Check if each requested scope is in the allowed scopes list
        return requestedScopesList.All(scope => allowedScopesList.Contains(scope));
    }

    private static string GenerateJSONWebToken(string identifier, string scope, JwtConfig jwtConfig,
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
            return null;
        }
    }
}