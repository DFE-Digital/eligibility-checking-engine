using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityStatusResponse
{
    public StatusValue Data { get; set; }
}
[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class StatusValue
{
    public string Status { get; set; }

    public string? Tier { get; set; }
}