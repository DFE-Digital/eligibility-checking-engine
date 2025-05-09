namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityItem
{
    public string NationalInsuranceNumber { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string DateOfBirth { get; set; } = string.Empty;

    public string? NationalAsylumSeekerServiceNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime Created { get; set; }
}

public class CheckEligibilityItemResponse
{
    public CheckEligibilityItem Data { get; set; } = null!;
    public CheckEligibilityResponseLinks Links { get; set; } = null!;
}