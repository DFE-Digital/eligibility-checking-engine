using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

#region Early Year Pupil Premium Type

public class CheckEligibilityRequestData_Eypp : IEligibilityServiceType
{
    public CheckEligibilityType Type => CheckEligibilityType.EarlyYearPupilPremium;

    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }
}


public class CheckEligibilityRequest_Eypp : ICheckEligibilityRequest<CheckEligibilityRequestData_Eypp>
{
    public CheckEligibilityRequestData_Eypp? Data { get; set; }
}

#endregion