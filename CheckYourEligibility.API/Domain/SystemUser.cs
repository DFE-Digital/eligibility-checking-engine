namespace CheckYourEligibility.API.Boundary.Requests;

public class SystemUser
{
    // Primary identifiers (OAuth2 standard names)
    public string? scope { get; set; }
    public string? grant_type { get; set; }

    // Made nullable to support both form fields and Basic Auth header
    public string? client_id { get; set; }
    public string? client_secret { get; set; }

    public string safe_client_id()
    {
        return client_id?.Split(':')[0] ?? string.Empty;
    }
}