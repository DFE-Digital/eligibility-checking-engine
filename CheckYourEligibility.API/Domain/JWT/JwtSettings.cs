namespace CheckYourEligibility.API.Domain;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public Dictionary<string, ClientSettings> Clients { get; set; } = new();
    public Dictionary<string, string> Users { get; set; } = new();
}