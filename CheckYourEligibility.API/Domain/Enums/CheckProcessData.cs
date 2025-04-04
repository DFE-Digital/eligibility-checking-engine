using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways;

public class CheckProcessData
{
    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string DateOfBirth { get; set; } = string.Empty;

    public string? NationalAsylumSeekerServiceNumber { get; set; }

    public CheckEligibilityType Type { get; set; }
}