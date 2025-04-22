using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class CheckEligibilityRequestDataBase : IEligibilityServiceType
{
    protected CheckEligibilityType baseType;
    public CheckEligibilityType Type => baseType;
}

public interface IEligibilityServiceType
{
}

#region FreeSchoolMeals Type

public class CheckEligibilityRequestData_Fsm : CheckEligibilityRequestDataBase
{
    public CheckEligibilityRequestData_Fsm()
    {
        baseType = CheckEligibilityType.FreeSchoolMeals;
    }

    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }

    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequest_Fsm
{
    public CheckEligibilityRequestData_Fsm? Data { get; set; }
}

public class CheckEligibilityRequestBulk_Fsm
{
    public IEnumerable<CheckEligibilityRequestData_Fsm> Data { get; set; }
}

#endregion