namespace CheckYourEligibility.Core.Domain;

public class JwtSettings
{
    public string Key { get; set; }
    public string Issuer { get; set; }
    public Dictionary<string, ClientSettings> Clients { get; set; } = new();
    public Dictionary<string, string> Users { get; set; } = new();
}