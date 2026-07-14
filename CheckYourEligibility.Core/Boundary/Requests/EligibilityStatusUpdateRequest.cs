using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.Core.Boundary.Requests;

public class EligibilityStatusUpdateRequest
{
    public EligibilityCheckStatusData? Data { get; set; }
}

public class EligibilityCheckStatusData
{
    public CheckEligibilityStatus Status { get; set; }
}