public class FosterFamiliesSearchItemResponse
{
    public string ChildName { get; set; } = string.Empty;

    public DateTime ChildDateOfBirth { get; set; }

    public string EligibilityCode { get; set; } = string.Empty;

    public string CarerName { get; set; } = string.Empty;
    public Guid CarerId { get; set; }

    public DateTime EligibilityConfirmedOn { get; set; }

    public string ReconfirmBetween { get; set; } = string.Empty;

    public DateTime GracePeriodEnds { get; set; }

    public string ReconfirmationStatus { get; set; } = string.Empty;
}