namespace CheckYourEligibility.Core.Boundary.Responses;

public class FosterFamilyCreatedResponse
{
    public Guid FosterCarerId { get; init; }

    public Guid FosterChildId { get; init; }
}