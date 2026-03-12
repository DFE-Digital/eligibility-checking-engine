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
    /// <summary>
    /// The type of eligibility check. This field is optional and determined by the endpoint path.
    /// When submitting requests, you can omit this field as the endpoint route automatically sets the correct type.
    /// </summary>
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

public class CheckMetaData {
    /// <summary>
    /// Source of check
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// API username or portal user email address
    /// </summary>
    public string? UserName { get; set; }
    /// <summary>
    /// ID of Organisation if found in scope
    /// else OrganisationID = 0
    /// </summary>
    public int? OrganisationID { get; set; }

    /// <summary>
    /// What type of organisation is making the check
    /// It can be local-authority, establishment multi-academy-trust
    /// unspecified (if no organisation ID is passed in scope)
    /// ambiguous (if more than one  organisation ID is passed in scope)
    /// </summary>
    public string? OrganisationType { get; set; }
}
public class CheckEligibilityRequestBulkBase
{
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
}

public class CheckEligibilityRequestBulk
{
    public IEnumerable<CheckEligibilityRequestBulkData> Data { get; set; }
    public CheckEligibilityRequestBulkBase? Meta { get; set; }
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

public class CheckEligibilityRequestWorkingFamiliesBulk : CheckEligibilityRequestBulk
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
                    EligibilityCode = "90246760112",
                    DateOfBirth = "2019-05-15",
                    NationalInsuranceNumber = "AB123456C",
                    ClientIdentifier = "12345",
                    GracePeriodEndDate = null,
                    LastName = "Johnson",
                    ValidityStartDate = null,
                    ValidityEndDate = null
                },
                new CheckEligibilityRequestWorkingFamiliesBulkData
                {
                    EligibilityCode = "90312345678",
                    DateOfBirth = "2020-08-22",
                    NationalInsuranceNumber = "CD987654B",
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
                NationalInsuranceNumber = "AB123456C",
                DateOfBirth = "2019-05-15",
                EligibilityCode = "90246760112",
                LastName = "Johnson",
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
                NationalInsuranceNumber = "NN123456C",
                LastName = "Tester",
                DateOfBirth = "2010-06-15"
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
                LastName = "Johnson",
                DateOfBirth = "2021-03-10"
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
                NationalInsuranceNumber = "CD987654B",
                LastName = "Williams",
                DateOfBirth = "2022-08-20"
            }
        };
    }
}

public class CheckFSMBulkModelExample : IExamplesProvider<CheckEligibilityRequestBulk>
{
    public CheckEligibilityRequestBulk GetExamples()
    {
        return new CheckEligibilityRequestBulk
        {
            Data = new List<CheckEligibilityRequestBulkData>
            {
                new CheckEligibilityRequestBulkData
                {
                    NationalInsuranceNumber = "NN123456C",
                    LastName = "Tester",
                    DateOfBirth = "2010-06-15",
                    ClientIdentifier = "12345"
                },
                new CheckEligibilityRequestBulkData
                {
                    NationalInsuranceNumber = "NN654321A",
                    LastName = "Ahmed",
                    DateOfBirth = "2011-09-22",
                    ClientIdentifier = "12346"
                }
            }
        };
    }
}

public class CheckEYPPBulkModelExample : IExamplesProvider<CheckEligibilityRequestBulk>
{
    public CheckEligibilityRequestBulk GetExamples()
    {
        return new CheckEligibilityRequestBulk
        {
            Data = new List<CheckEligibilityRequestBulkData>
            {
                new CheckEligibilityRequestBulkData
                {
                    NationalInsuranceNumber = "AB123456C",
                    LastName = "Johnson",
                    DateOfBirth = "2021-03-10",
                    ClientIdentifier = "12345"
                },
                new CheckEligibilityRequestBulkData
                {
                    NationalInsuranceNumber = "CD654321B",
                    LastName = "Patel",
                    DateOfBirth = "2020-11-05",
                    ClientIdentifier = "12346"
                }
            }
        };
    }
}

public class Check2YOBulkModelExample : IExamplesProvider<CheckEligibilityRequestBulk>
{
    public CheckEligibilityRequestBulk GetExamples()
    {
        return new CheckEligibilityRequestBulk
        {
            Data = new List<CheckEligibilityRequestBulkData>
            {
                new CheckEligibilityRequestBulkData
                {
                    NationalInsuranceNumber = "CD987654B",
                    LastName = "Williams",
                    DateOfBirth = "2022-08-20",
                    ClientIdentifier = "12345"
                },
                new CheckEligibilityRequestBulkData
                {
                    NationalInsuranceNumber = "EF654321C",
                    LastName = "Khan",
                    DateOfBirth = "2022-12-15",
                    ClientIdentifier = "12346"
                }
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
        where T : CheckEligibilityRequestBulk
    {
        var modelData = (model as dynamic).Data;

        foreach (var item in modelData)
        {
            if (item.Type != routeType)
                item.Type = routeType;
        }

        return model;
    }
}