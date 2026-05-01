// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationUpdateResponse
{
    public ApplicationUpdateDataResponse Data { get; set; }
}
public class ApplicationUpdateDataResponse
{
    public string Status { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Tier { get; set; }
    public int? EstablishmentUrn { get; set; }
}
