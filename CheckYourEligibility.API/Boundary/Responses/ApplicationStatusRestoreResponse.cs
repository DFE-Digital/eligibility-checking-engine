using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationStatusRestoreResponse
{
    public ApplicationStatusRestoreResponseData Data { get; set; }
}

public class ApplicationStatusRestoreResponseData
{
    public string Status { get; set; }
    public EligibilityTier? Tier { get; set; }

    public DateTime Updated { get; set; }
}