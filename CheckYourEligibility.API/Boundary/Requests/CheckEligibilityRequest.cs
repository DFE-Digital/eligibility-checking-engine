using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class CheckEligibilityRequestDataBase : IEligibilityServiceType
{
    // Set the default type to FreeSchoolMeals instead of None
    protected CheckEligibilityType baseType = CheckEligibilityType.FreeSchoolMeals;

    public CheckEligibilityType Type
    {
        get => baseType;
        set => baseType = value != CheckEligibilityType.None ? value : CheckEligibilityType.FreeSchoolMeals;
    }
}

public interface IEligibilityServiceType
{
}

#region FreeSchoolMeals Type

public class CheckEligibilityRequestData_Fsm : CheckEligibilityRequestDataBase
{
    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }

    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequestBulkData_Fsm : CheckEligibilityRequestData_Fsm
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequest_Fsm
{
    public CheckEligibilityRequestData_Fsm? Data { get; set; }
}

public class CheckEligibilityRequestBulk_Fsm
{
    public IEnumerable<CheckEligibilityRequestBulkData_Fsm> Data { get; set; }
}

#endregion