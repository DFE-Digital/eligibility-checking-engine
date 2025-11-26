// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class ApplicationUpdateRequest
{
    public ApplicationUpdateData? Data { get; set; }
}

public class ApplicationUpdateData
{
    public ApplicationStatus? Status { get; set; }
    public int? EstablishmentUrn { get; set; }
}
