using System.Diagnostics.CodeAnalysis;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace CheckYourEligibility.API.Gateways.CsvImport;

[ExcludeFromCodeCoverage]
public class EstablishmentPrivateBetaRow
{
    public int EstablishmentId { get; set; }
    public bool InPrivateBeta { get; set; }
}

[ExcludeFromCodeCoverage]
public class EstablishmentPrivateBetaRowMap : ClassMap<EstablishmentPrivateBetaRow>
{
    public EstablishmentPrivateBetaRowMap()
    {
        Map(m => m.EstablishmentId).Name("School URN");
        Map(m => m.InPrivateBeta).Name("In Private Beta").TypeConverter<YesNoBooleanConverter>();
    }
}

[ExcludeFromCodeCoverage]
public class YesNoBooleanConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
    }

    public override string ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is bool boolValue)
            return boolValue ? "Yes" : "No";

        return "No";
    }
}
