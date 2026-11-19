using CheckYourEligibility.Core.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class PostCheckResult
{
    public string Id { get; set; }

    public EligibilityTier? Tier { get; set; }
    public CheckEligibilityStatus Status { get; set; }
}