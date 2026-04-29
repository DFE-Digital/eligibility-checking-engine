using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationStatusRestoreResponse
{
    public ApplicationStatusRestoreResponseData Data { get; set; }
}

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class ApplicationStatusRestoreResponseData
{
    public string Status { get; set; }
    public string? Tier { get; set; }

    public DateTime Updated { get; set; }
}