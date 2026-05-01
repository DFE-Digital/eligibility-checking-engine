using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityStatusResponse
{
    public StatusValue Data { get; set; }
}
public class StatusValue
{
    public string Status { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Tier { get; set; }
}