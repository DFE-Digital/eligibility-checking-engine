using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration;

namespace CheckYourEligibility.Core.Domain.CsvImport;

[ExcludeFromCodeCoverage]
public class HomeOfficeRow
{
    public string Nas { get; set; }
    public string Dob { get; set; }
    public string Surname { get; set; }
}

[ExcludeFromCodeCoverage]
public class HomeOfficeRowMap : ClassMap<HomeOfficeRow>
{
    public HomeOfficeRowMap()
    {
        Map(m => m.Nas).Index(0);
        Map(m => m.Dob).Index(1);
        Map(m => m.Surname).Index(2);
    }
}