using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.Core.Domain.Constants;
using CsvHelper.Configuration;

namespace CheckYourEligibility.Core.Domain.CsvImport;

[ExcludeFromCodeCoverage]
public class EstablishmentRow
{
    public int Urn { get; set; }
    public int LaCode { get; set; }
    public string LaName { get; set; }
    public string LaRegion { get; set; }
    public string EstablishmentName { get; set; }
    public string Postcode { get; set; }
    public string Street { get; set; }
    public string Locality { get; set; }
    public string Town { get; set; }
    public string County { get; set; }
    public string Status { get; set; }
    public string Type { get; set; }
}

[ExcludeFromCodeCoverage]
public class EstablishmentRowMap : ClassMap<EstablishmentRow>
{
    public EstablishmentRowMap()
    {
        Map(m => m.Urn).Name(CSVHeaders.URN);
        Map(m => m.LaCode).Name(CSVHeaders.LA_Code);
        Map(m => m.LaName).Name(CSVHeaders.LA_Name);
        Map(m => m.LaRegion).Name(CSVHeaders.LA_Region);
        Map(m => m.EstablishmentName).Name(CSVHeaders.EstablishmentName);
        Map(m => m.Postcode).Name(CSVHeaders.Postcode);
        Map(m => m.Street).Name(CSVHeaders.Street);
        Map(m => m.Locality).Name(CSVHeaders.Locality);
        Map(m => m.Town).Name(CSVHeaders.Town);
        Map(m => m.County).Name(CSVHeaders.County_Name);
        Map(m => m.Status).Name(CSVHeaders.EstablishmentStatus_Name);
        Map(m => m.Type).Name(CSVHeaders.TypeOfEstablishment_Name);
    }
}