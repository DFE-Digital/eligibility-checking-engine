namespace CheckYourEligibility.Api.Boundary.Responses;

public class JwtAuthResponse
{
    public string access_token { get; set; } = string.Empty;
    public int expires_in { get; set; }
    public string token_type { get; set; } = string.Empty;
}