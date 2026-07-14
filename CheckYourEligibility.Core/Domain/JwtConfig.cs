namespace CheckYourEligibility.Core.Domain;

public class JwtConfig
{
    public string Key { get; set; }
    public string Issuer { get; set; }
    public string ExpectedSecret { get; set; }
    public string AllowedScopes { get; set; }
}