using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class PostCheckResult
{
    public string Id { get; set; }

    public EligibilityTier? Tier { get; set; }
    public CheckEligibilityStatus Status { get; set; }
}