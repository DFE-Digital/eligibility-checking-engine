using CheckYourEligibility.Core.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses;

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