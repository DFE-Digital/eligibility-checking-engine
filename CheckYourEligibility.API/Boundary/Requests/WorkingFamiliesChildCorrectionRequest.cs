namespace CheckYourEligibility.API.Boundary.Requests;

public class WorkingFamiliesChildCorrectionRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? DateOfBirth { get; set; }

    public string? Postcode { get; set; }

    public string? ExpectedVersion { get; set; }
}