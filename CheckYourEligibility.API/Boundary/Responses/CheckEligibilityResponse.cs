namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityResponse
{
    public StatusValue Data { get; set; } = new();
    public CheckEligibilityResponseLinks Links { get; set; } = new();
}

public class CheckEligibilityResponseBulk
{
    public StatusValue Data { get; set; } = new();
    public CheckEligibilityResponseBulkLinks Links { get; set; } = new();
}

public class CheckEligibilityResponseBulkLinks
{
    public string Get_Progress_Check { get; set; } = string.Empty;
    public string Get_BulkCheck_Results { get; set; } = string.Empty;
}