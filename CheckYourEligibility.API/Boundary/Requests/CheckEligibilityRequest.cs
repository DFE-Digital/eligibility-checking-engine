using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Filters;

namespace CheckYourEligibility.API.Boundary.Requests;

public class CheckEligibilityRequestDataBase : IEligibilityServiceType
{
    // Set the default type to FreeSchoolMeals instead of None
    protected CheckEligibilityType baseType = CheckEligibilityType.FreeSchoolMeals;
    //public int? Sequence { get; set; }

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

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class CheckEligibilityRequestData : CheckEligibilityRequestDataBase
{
    public string? NationalInsuranceNumber { get; set; }
    public string? LastName { get; set; }
    public string DateOfBirth { get; set; }
    public string? NationalAsylumSeekerServiceNumber { get; set; } 
    public string? EligibilityCode { get; set; }
}

public class CheckEligibilityRequestBulkData : CheckEligibilityRequestData
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequest
{
    public CheckEligibilityRequestData? Data { get; set; }
}

public class CheckWFModelExample : IExamplesProvider<CheckEligibilityRequest>
{
    public CheckEligibilityRequest GetExamples() {
        return new CheckEligibilityRequest
        {
            Data = new CheckEligibilityRequestData
            {
                Type = CheckEligibilityType.WorkingFamilies,
                NationalInsuranceNumber = "AB123456C",
                NationalAsylumSeekerServiceNumber = null,
                LastName = null,
                DateOfBirth = "2024-01-01",
                EligibilityCode = "50012345678"
            }
        };
    }
}
public class CheckFSMModelExample : IExamplesProvider<CheckEligibilityRequest>
{
    public CheckEligibilityRequest GetExamples()
    {
        return new CheckEligibilityRequest
        {
            Data = new CheckEligibilityRequestData
            {
                NationalInsuranceNumber = "AB123456C",
                NationalAsylumSeekerServiceNumber = "AB123456C",
                LastName = "Smith",
                DateOfBirth = "2024-01-01",
                EligibilityCode = null
            }
        };
    }
}

public class CheckEligibilityRequestBulk
{
    public string? ClientIdentifier { get; set; }
    public string? Filename { get; set; }
    public string? SubmittedBy{ get; set; }
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
    public static CheckEligibilityRequestBulk CreateBulkFromGeneric(CheckEligibilityRequestBulk model, CheckEligibilityType routeType)
    {
        foreach (var item in model.Data)
        {
            if (item.Type != routeType)
                item.Type = routeType;

            item.ClientIdentifier = model.ClientIdentifier;
        }
        
        return model;
    }
}