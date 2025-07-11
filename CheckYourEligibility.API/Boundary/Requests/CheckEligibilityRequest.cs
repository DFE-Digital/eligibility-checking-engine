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
    public string? NationalInsuranceNumber { get; set; }

}

public interface IEligibilityServiceType
{
    public CheckEligibilityType Type { get; set;}
}

#region FreeSchoolMeals Type
[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class CheckEligibilityRequestData : CheckEligibilityRequestDataBase
{
    public string? LastName { get; set; }
    public string DateOfBirth { get; set; }
    public string? NationalAsylumSeekerServiceNumber { get; set; } 

}
[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class CheckEligibilityRequestWorkingFamiliesData : CheckEligibilityRequestDataBase
{
    public string EligibilityCode { get; set; }
    public string? ValidityStartDate { get; set; }
    public string? ValidityEndDate { get; set; }
    public string? GracePeriodEndDate { get; set; }  
    public string? ParentLastName { get; set; }  
    public string ChildDateOfBirth { get; set; }

}
public class CheckEligibilityRequestBulkData : CheckEligibilityRequestData
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequest<T> where T : IEligibilityServiceType
{
    public T? Data { get; set; }
}

public class CheckWFModelExample : IExamplesProvider<CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>>
{
    public CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData> GetExamples() {
        return new CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>
        {
            Data = new CheckEligibilityRequestWorkingFamiliesData
            {
                Type = CheckEligibilityType.WorkingFamilies,
                NationalInsuranceNumber = "AB123456C",
                ChildDateOfBirth = "2024-01-01",
                EligibilityCode = "50012345678",
                ParentLastName = null,
                ValidityStartDate = null,
                ValidityEndDate = null,
                GracePeriodEndDate = null
            }
        };
    }
}
public class CheckFSMModelExample : IExamplesProvider<CheckEligibilityRequest<CheckEligibilityRequestData>>
{
    public CheckEligibilityRequest<CheckEligibilityRequestData> GetExamples()
    {
        return new CheckEligibilityRequest<CheckEligibilityRequestData>
        {
            Data = new CheckEligibilityRequestData
            {
                NationalInsuranceNumber = "AB123456C",
                NationalAsylumSeekerServiceNumber = "AB123456C",
                LastName = "Smith",
                DateOfBirth = "2024-01-01"
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
    public static CheckEligibilityRequest<T> CreateFromGeneric<T>(CheckEligibilityRequest<T> model, CheckEligibilityType routeType) where T : IEligibilityServiceType
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

                          
        }
        
        return model;
    }
}