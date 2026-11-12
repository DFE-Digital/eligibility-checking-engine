using CheckYourEligibility.Core.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class ApplicationUpdateResponse
{
    public ApplicationUpdateDataResponse Data { get; set; }
}
public class ApplicationUpdateDataResponse
{
    public string Status { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Tier { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? EligibilityEndDate { get; set; }

    public int? EstablishmentUrn { get; set; }
}
