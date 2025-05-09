using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Responses;

public class PostCheckResult
{
    public string Id { get; set; } = string.Empty;
    public CheckEligibilityStatus Status { get; set; }
}