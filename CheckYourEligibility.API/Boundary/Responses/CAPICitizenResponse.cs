using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class CAPICitizenResponse: CAPIClaimResponse
    {
           public string? Guid { get; set; }
    }
}
