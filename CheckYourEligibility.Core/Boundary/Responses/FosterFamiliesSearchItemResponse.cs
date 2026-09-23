using System.Text.Json.Serialization;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class FosterFamiliesSearchItemResponse
{
    public string ChildName { get; set; } = string.Empty;

    public DateTime ChildDateOfBirth { get; set; }

    public string EligibilityCode { get; set; } = string.Empty;

    public string CarerName { get; set; } = string.Empty;

    public Guid FosterCarerId { get; set; }

    public Guid FosterChildId { get; set; }

    public DateTime ValidityStartDate { get; set; }

    public ReconfirmationProperties ReconfirmationProperties { get; set; }

    public DateTime GracePeriodEndDate { get; set; }

    [JsonIgnore]
    public DateTime? ValidityEndDate { get; set; }

}