public class FosterFamilyRequest
{
    public FosterCarerRequest FosterCarer { get; set; } = null!;

    public bool HasPartner { get; set; }

    public FosterPartnerRequest? Partner { get; set; }

    public FosterChildRequest FosterChild { get; set; } = null!;

    public DateTime SubmissionDate { get; set; }
}