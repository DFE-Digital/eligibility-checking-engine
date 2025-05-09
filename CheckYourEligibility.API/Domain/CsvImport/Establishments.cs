using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Constants;
using CsvHelper.Configuration;

namespace CheckYourEligibility.API.Gateways.CsvImport;

[ExcludeFromCodeCoverage]
public class EstablishmentRow
{
    public int Urn { get; set; }
    public int LaCode { get; set; }
    public string LaName { get; set; } = string.Empty;
    public string EstablishmentName { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string Town { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class EstablishmentRowMap : ClassMap<EstablishmentRow>
{
    public EstablishmentRowMap()
    {
        Map(m => m.Urn).Name(CSVHeaders.URN);
        Map(m => m.LaCode).Name(CSVHeaders.LA_Code);
        Map(m => m.LaName).Name(CSVHeaders.LA_Name);
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