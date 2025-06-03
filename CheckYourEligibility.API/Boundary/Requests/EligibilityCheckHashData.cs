// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class EligibilityCheckHashData
{
    public CheckEligibilityStatus Outcome { get; set; }
}