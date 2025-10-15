using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Filters;

namespace CheckYourEligibility.API.Boundary.Requests;

public class CheckEligibilityRequestDataBase : IEligibilityServiceType
{
    // Set the default type to FreeSchoolMeals instead of None
    protected CheckEligibilityType baseType = CheckEligibilityType.FreeSchoolMeals;
    //public int? Sequence { get; set; }

    public string? DateOfBirth { get; set; }
    public string? LastName { get; set; }
    public CheckEligibilityType Type
    {
        get => baseType;
        set => baseType = value != CheckEligibilityType.None ? value : CheckEligibilityType.FreeSchoolMeals;
    }

    public string? NationalInsuranceNumber { get; set; }
}

public interface IEligibilityServiceType
{
    public CheckEligibilityType Type { get; set; }
}

public class CheckEligibilityRequestBulkBase
{
    public string? ClientIdentifier { get; set; }
    public string? Filename { get; set; }
    public string? SubmittedBy { get; set; }
    public int? LocalAuthorityId { get; set; }
}

#region FreeSchoolMeals,  EarlyYearPupilPremium, TwoYearOffer type

public class CheckEligibilityRequestData : CheckEligibilityRequestDataBase
{
    public string? NationalAsylumSeekerServiceNumber { get; set; }
}

public class CheckEligibilityRequestBulkData : CheckEligibilityRequestData
{
    public string? ClientIdentifier { get; set; }
    public string? Filename { get; set; }
    public string? SubmittedBy { get; set; }
}

public class CheckEligibilityRequestBulk : CheckEligibilityRequestBulkBase
{
    public IEnumerable<CheckEligibilityRequestBulkData> Data { get; set; }
}

#endregion

#region Working Families

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class CheckEligibilityRequestWorkingFamiliesData : CheckEligibilityRequestDataBase
{

    public string? EligibilityCode { get; set; }
    public string? ValidityStartDate { get; set; }
    public string? ValidityEndDate { get; set; }
    public string? GracePeriodEndDate { get; set; }
}

public class CheckEligibilityRequestWorkingFamiliesBulk : CheckEligibilityRequestBulkBase
{
    public IEnumerable<CheckEligibilityRequestWorkingFamiliesBulkData> Data { get; set; }
}

public class CheckEligibilityRequestWorkingFamiliesBulkData : CheckEligibilityRequestWorkingFamiliesData
{
    public string? ClientIdentifier { get; set; }
    public string? Filename { get; set; }
    public string? SubmittedBy { get; set; }
}

#endregion

public class CheckEligibilityRequest<T> where T : IEligibilityServiceType
{
    public T? Data { get; set; }
}

public class CheckWFBulkModelExample : IExamplesProvider<CheckEligibilityRequestWorkingFamiliesBulk>
{
    public CheckEligibilityRequestWorkingFamiliesBulk GetExamples()
    {
        return new CheckEligibilityRequestWorkingFamiliesBulk
        {
            Data = new List<CheckEligibilityRequestWorkingFamiliesBulkData>
            {
                new CheckEligibilityRequestWorkingFamiliesBulkData
                {
                    Type = CheckEligibilityType.WorkingFamilies,
                    EligibilityCode = "50012345678",
                    DateOfBirth = "2022-01-01",
                    NationalInsuranceNumber = "AB123456C",
                    ClientIdentifier = "12345",
                    GracePeriodEndDate = null,
                    LastName = "Smith",
                    ValidityStartDate = null,
                    ValidityEndDate = null
                },
                new CheckEligibilityRequestWorkingFamiliesBulkData
                {
                    Type = CheckEligibilityType.WorkingFamilies,
                    EligibilityCode = "50012345679",
                    DateOfBirth = "2022-01-02",
                    NationalInsuranceNumber = "AB123456D",
                    ClientIdentifier = "12346",
                    GracePeriodEndDate = null,
                    LastName = "Smith",
                    ValidityStartDate = null,
                    ValidityEndDate = null
                }
            }
        };
    }
}

public class
    CheckWFModelExample : IExamplesProvider<CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>>
{
    public CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData> GetExamples()
    {
        return new CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>
        {
            Data = new CheckEligibilityRequestWorkingFamiliesData
            {
                Type = CheckEligibilityType.WorkingFamilies,
                NationalInsuranceNumber = "AB123456C",
                DateOfBirth = "2024-01-01",
                EligibilityCode = "50012345678",
                LastName = "Smith",
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
                DateOfBirth = "2024-01-01",
                Type = CheckEligibilityType.FreeSchoolMeals,
            }
        };
    }
}

public class CheckEYPPModelExample : IExamplesProvider<CheckEligibilityRequest<CheckEligibilityRequestData>>
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
                DateOfBirth = "2024-01-01",
                Type = CheckEligibilityType.EarlyYearPupilPremium,
            }
        };
    }
}

public class Check2YOModelExample : IExamplesProvider<CheckEligibilityRequest<CheckEligibilityRequestData>>
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
                DateOfBirth = "2024-01-01",
                Type = CheckEligibilityType.TwoYearOffer,
            }
        };
    }
}

public static class EligibilityModelFactory
{
    public static CheckEligibilityRequest<T> CreateFromGeneric<T>(CheckEligibilityRequest<T> model,
        CheckEligibilityType routeType) where T : IEligibilityServiceType
    {
        if (model.Data.Type != routeType)
            model.Data.Type = routeType;

        return model;
    }
}

public static class EligibilityBulkModelFactory
{
    public static T CreateBulkFromGeneric<T>(T model, CheckEligibilityType routeType)
        where T : CheckEligibilityRequestBulkBase
    {
        var modelData = (model as dynamic).Data;

        foreach (var item in modelData)
        {
            if (item.Type != routeType)
                item.Type = routeType;
            item.Filename = model.Filename;
            item.SubmittedBy = model.SubmittedBy;
        }

        return model;
    }
}