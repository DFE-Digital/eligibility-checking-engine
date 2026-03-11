using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

/// <summary>
/// Response body returned by the EFE PUT eligibility-events endpoint.
/// Uses explicit JSON property names to guarantee casing regardless of serialiser settings.
/// </summary>
public class WorkingFamiliesEventResponse
{
    [JsonProperty("eligibilityCode")]
    public string? EligibilityCode { get; set; }

    [JsonProperty("hMRCEligibilityEventId")]
    public string? HMRCEligibilityEventId { get; set; }

    [JsonProperty("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonProperty("deletedDateTime")]
    public DateTime? DeletedDateTime { get; set; }

    [JsonProperty("createdDateTime")]
    public DateTime? CreatedDateTime { get; set; }

    [JsonProperty("eventDateTime")]
    public DateTime? EventDateTime { get; set; }
}
