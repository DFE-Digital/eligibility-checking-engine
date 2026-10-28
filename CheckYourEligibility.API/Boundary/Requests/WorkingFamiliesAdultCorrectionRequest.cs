namespace CheckYourEligibility.API.Boundary.Requests;

public class WorkingFamiliesAdultCorrectionRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? DateOfBirth { get; set; }

    public string? NationalInsuranceNumber { get; set; }
}