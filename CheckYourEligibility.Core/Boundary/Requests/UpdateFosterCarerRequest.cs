namespace CheckYourEligibility.Core.Boundary.Requests;

public class UpdateFosterCarerRequest
{
    public FosterCarerRequest? FosterCarerRequest { get; set; }
    public FosterPartnerRequest? FosterPartnerRequest { get; set; }
}