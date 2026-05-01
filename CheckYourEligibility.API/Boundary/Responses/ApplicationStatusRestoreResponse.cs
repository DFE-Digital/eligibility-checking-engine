using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationStatusRestoreResponse
{
    public ApplicationStatusRestoreResponseData Data { get; set; }
}

public class ApplicationStatusRestoreResponseData
{
    public string Status { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Tier { get; set; }

    public DateTime Updated { get; set; }
}