namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityItem
{
    public string NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }

    public string NationalAsylumSeekerServiceNumber { get; set; }
    
    public string? ClientIdentifier { get; set; }

    public string Status { get; set; }

    public DateTime Created { get; set; }
}

public class CheckEligibilityItemResponse
{
    public CheckEligibilityItem Data { get; set; }
    public CheckEligibilityResponseLinks Links { get; set; }
}