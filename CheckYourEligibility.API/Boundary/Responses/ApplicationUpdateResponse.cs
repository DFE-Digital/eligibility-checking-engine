// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationUpdateResponse
{
    public ApplicationUpdateDataResponse Data { get; set; }
}

public class ApplicationUpdateDataResponse
{
    public string Status { get; set; }
    public EligibilityTier? Tier { get; set; }
    public int? EstablishmentUrn { get; set; }
}
