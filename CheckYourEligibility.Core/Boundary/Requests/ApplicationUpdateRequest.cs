using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.Core.Boundary.Requests;

public class ApplicationUpdateRequest
{
    public ApplicationUpdateData? Data { get; set; }
}

public class ApplicationUpdateData
{
    public ApplicationStatus? Status { get; set; }

    public EligibilityTier? Tier { get; set; }
    
    public int? EstablishmentUrn { get; set; }
}
