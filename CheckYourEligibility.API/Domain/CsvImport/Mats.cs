using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration;

namespace CheckYourEligibility.API.Gateways.CsvImport;

[ExcludeFromCodeCoverage]
public class MatRow
{
    public int GroupUID { get; set; }
    public string GroupName { get; set; }
    public int AcademyURN { get; set; }
    public string AcademyName { get; set; }
}

[ExcludeFromCodeCoverage]
public class MatRowMap : ClassMap<MatRow>
{
    public MatRowMap()
    {
        Map(m => m.GroupUID).Name("Group UID");
        Map(m => m.GroupName).Name("Group Name");
        Map(m => m.AcademyURN).Name("Academy URN");
        Map(m => m.AcademyName).Name("Trust Academy Establishment Name");
    }
}