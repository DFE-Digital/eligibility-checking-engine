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

public class CheckEligibilityRequestData : CheckEligibilityRequestDataBase
{
    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string DateOfBirth { get; set; }

    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequestBulkData : CheckEligibilityRequestData
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequest
{
    public CheckEligibilityRequestData? Data { get; set; }
}

public class CheckEligibilityRequestBulk
{
    public IEnumerable<CheckEligibilityRequestBulkData> Data { get; set; }
}

#endregion
public static class EligibilityModelFactory
{
    public static CheckEligibilityRequest CreateFromGeneric(CheckEligibilityRequest model, CheckEligibilityType routeType)
    {
        if (model.Data.Type != routeType)
            model.Data.Type = routeType;

        return model;
    }
}

public static class EligibilityBulkModelFactory
{
    public static CheckEligibilityRequestBulk CreateFromGeneric(CheckEligibilityRequestBulk model, CheckEligibilityType routeType)
    {
        foreach (var item in model.Data)
        {
            if (item.Type != routeType)
                item.Type = routeType;
        }

        return model;
    }
}