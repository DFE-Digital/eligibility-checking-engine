using CheckYourEligibility.Core.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class CheckEligibilityStatusResponse
{
    public StatusValue Data { get; set; }
}
public class StatusValue
{
    public string Status { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Tier { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ErrorCode { get; set; }
}