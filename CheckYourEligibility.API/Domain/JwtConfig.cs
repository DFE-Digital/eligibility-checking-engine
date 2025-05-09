namespace CheckYourEligibility.API.Domain;

public class JwtConfig
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string ExpectedSecret { get; set; } = string.Empty;
    public string AllowedScopes { get; set; } = string.Empty;
}