using System.Text.Json.Serialization;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

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

    [JsonIgnore]
    public DateTime? ValidityEndDate { get; set; }

    public string ReconfirmationStatus { get; set; }
}