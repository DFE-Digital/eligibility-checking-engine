public class EligibilityCodeResponse
{
    public string EligibilityCode { get; init; }
    public string Status { get; init; }
    public DateTime EligibilityConfirmed { get; init; }
    public string ReconfirmBetween { get; init; }
    public DateTime GracePeriodEndDate { get; init; } 
}