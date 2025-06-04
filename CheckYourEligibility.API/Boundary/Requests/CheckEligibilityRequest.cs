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
public class CheckEligibilityRequestDataBase : IEligibilityServiceType
{
    protected CheckEligibilityType baseType;
    public int? Sequence { get; set; }
}

public interface IEligibilityServiceType
{
    CheckEligibilityType Type { get; }
    string LastName { get; set; }
    string DateOfBirth { get; set; }
    string? NationalInsuranceNumber { get; set; }
    string? NationalAsylumSeekerServiceNumber { get; set; }
}

public interface ICheckEligibilityRequest
{
    CheckEligibilityRequestData? Data { get; set; }
}

public static class EligibilityModelFactory
{
    public static CheckEligibilityRequest CreateFromGeneric(CheckEligibilityRequest model, CheckEligibilityType routeType)
    {
        if (model.Data.Type != routeType)
            model.Data.Type = routeType;

        return model;
    }
}

public class CheckEligibilityRequestData : IEligibilityServiceType
{
    public CheckEligibilityType Type { get; set; } 
    public string LastName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string? NationalInsuranceNumber { get; set; }
    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequest : ICheckEligibilityRequest
{
    public CheckEligibilityRequestData? Data { get; set; }
}