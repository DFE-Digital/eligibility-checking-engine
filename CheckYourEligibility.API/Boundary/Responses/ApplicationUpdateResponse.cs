// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationUpdateResponse
{
    public ApplicationUpdateDataResponse Data { get; set; }
}
[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class ApplicationUpdateDataResponse
{
    public string Status { get; set; }
    public string? Tier { get; set; }
    public int? EstablishmentUrn { get; set; }
}
