namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityResponse
{
    public StatusValue Data { get; set; }
    public CheckEligibilityResponseLinks Links { get; set; }
}

public class CheckEligibilityResponseBulk
{
    public StatusValue Data { get; set; }
    public CheckEligibilityResponseBulkLinks Links { get; set; }
}

public class CheckEligibilityResponseBulkLinks
{
    public string Get_Progress_Check { get; set; }
    public string Get_BulkCheck_Results { get; set; }
}