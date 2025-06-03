using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

#region 2 Year Offer Type

public class CheckEligibilityRequestData_2yo : IEligibilityServiceType
{
    public CheckEligibilityType Type => CheckEligibilityType.TwoYearOffer;

    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }
    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequest_2yo : ICheckEligibilityRequest<CheckEligibilityRequestData_2yo>
{
    public CheckEligibilityRequestData_2yo? Data { get; set; }
}

#endregion