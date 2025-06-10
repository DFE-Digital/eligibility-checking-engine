using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration;

namespace CheckYourEligibility.API.Gateways.CsvImport;

[ExcludeFromCodeCoverage]
public class ApplicationBulkImportRow
{
    public string ParentFirstName { get; set; }
    public string ParentSurname { get; set; }
    public string ParentDOB { get; set; }
    public string ParentNino { get; set; }
    public string ParentEmail { get; set; }
    public string ChildFirstName { get; set; }
    public string ChildSurname { get; set; }
    public string ChildSchoolUrn { get; set; }
    public string EligibilityEndDate { get; set; }
}

[ExcludeFromCodeCoverage]
public class ApplicationBulkImportRowMap : ClassMap<ApplicationBulkImportRow>
{
    public ApplicationBulkImportRowMap()
    {
        Map(m => m.ParentFirstName).Name("Parent First Name");
        Map(m => m.ParentSurname).Name("Parent Surname");
        Map(m => m.ParentDOB).Name("Parent DOB");
        Map(m => m.ParentNino).Name("Parent Nino");
        Map(m => m.ParentEmail).Name("Parent Email Address");
        Map(m => m.ChildFirstName).Name("Child First Name");
        Map(m => m.ChildSurname).Name("Child Surname");
        Map(m => m.ChildSchoolUrn).Name("Child School URN");
        Map(m => m.EligibilityEndDate).Name("Eligibility end date");
    }
}
