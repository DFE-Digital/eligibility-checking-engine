namespace CheckYourEligibility.API.Domain;

public class ClientSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}