using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration;

namespace CheckYourEligibility.Core.Domain.CsvImport;

[ExcludeFromCodeCoverage]
public class HomeOfficeRow
{
    public string FirstName { get; set; }
    public string Surname { get; set; }
    public string Dob { get; set; }
    public string AsylumSeekerReferenceNumber { get; set; }
    public string PortReferenceNumber { get; set; }
}

[ExcludeFromCodeCoverage]
public class HomeOfficeRowMap : ClassMap<HomeOfficeRow>
{
    public HomeOfficeRowMap()
    {
        Map(m => m.AsylumSeekerReferenceNumber).Name("AS Ref");
        Map(m => m.PortReferenceNumber).Name("Port Ref");
        Map(m => m.FirstName).Name("Given Name");
        Map(m => m.Surname).Name("Family Name");
        Map(m => m.Dob).Name("DOB");
    }
}