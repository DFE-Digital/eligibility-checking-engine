using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

//public class CheckEligibilityRequestDataBase : IEligibilityServiceType
//{
//    // Set the default type to FreeSchoolMeals instead of None
//    protected CheckEligibilityType baseType = CheckEligibilityType.FreeSchoolMeals;
    
//    public CheckEligibilityType Type 
//    { 
//        get => baseType; 
//        set => baseType = value != CheckEligibilityType.None ? value : CheckEligibilityType.FreeSchoolMeals;
//    }
//}

public interface IEligibilityServiceType: IHasNationalInsurance, IHasAsylumSeekerNumber
{
    CheckEligibilityType Type { get; }
    string LastName { get; set; }
    string DateOfBirth { get; set; }
    string? NationalInsuranceNumber { get; set; }
    string? NationalAsylumSeekerServiceNumber { get; set; }
}
public interface IHasNationalInsurance
{
    string? NationalInsuranceNumber { get; set; }
}

public interface IHasAsylumSeekerNumber
{
    string? NationalAsylumSeekerServiceNumber { get; set; }
}

public interface ICheckEligibilityRequest<TItem>
{
    TItem? Data {get;set;}
}

#region FreeSchoolMeals Type

public class CheckEligibilityRequestData_Fsm : IEligibilityServiceType
{
    public CheckEligibilityType Type => CheckEligibilityType.FreeSchoolMeals;

    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }

    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequest_Fsm : ICheckEligibilityRequest<CheckEligibilityRequestData_Fsm>
{
    public CheckEligibilityRequestData_Fsm? Data { get; set; }
}

#endregion