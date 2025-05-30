// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class EligibilityStatusUpdateRequest
{
    public EligibilityCheckStatusData? Data { get; set; }
}

public class EligibilityCheckStatusData
{
    public CheckEligibilityStatus Status { get; set; }
}