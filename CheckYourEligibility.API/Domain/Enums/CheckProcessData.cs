using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways;

public class CheckProcessData
{
    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string EligibilityCode { get; set; }

    public string DateOfBirth { get; set; }

    public string? NationalAsylumSeekerServiceNumber { get; set; }

    public string? ClientIdentifier { get; set; }

    public CheckEligibilityType Type { get; set; }
}