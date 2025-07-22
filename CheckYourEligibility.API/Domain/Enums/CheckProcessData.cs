using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways;

public class CheckProcessData
{
    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }
    public string ParentLastName { get; set; }
    
    public string EligibilityCode { get; set; }
    public string ValidityStartDate { get; set; }
    public string ValidityEndDate { get; set; }
    public string GracePeriodEndDate { get; set; }
    public string DateOfBirth { get; set; }
    public string ChildDateOfBirth { get; set; }

    public string? NationalAsylumSeekerServiceNumber { get; set; }

    public string? ClientIdentifier { get; set; }

    public CheckEligibilityType Type { get; set; }
}