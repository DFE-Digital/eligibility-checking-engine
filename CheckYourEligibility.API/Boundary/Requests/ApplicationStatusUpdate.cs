// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class ApplicationStatusUpdateRequest
{
    public ApplicationStatusData? Data { get; set; }
}

public class ApplicationStatusData
{
    public ApplicationStatus Status { get; set; }
}