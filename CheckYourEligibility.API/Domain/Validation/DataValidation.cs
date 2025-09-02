using System.Globalization;
using System.Text.RegularExpressions;

namespace CheckYourEligibility.API.Domain.Validation;

internal static class DataValidation
{
    internal static bool BeAValidNi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.ToUpper();
        var regexString =
            @"^(?!BG)(?!GB)(?!NK)(?!KN)(?!TN)(?!NT)(?!ZZ)(?:[A-CEGHJ-PR-TW-Z][A-CEGHJ-NPR-TW-Z])(?:\s*\d\s*){6}([A-D]|\s)$";
        var rg = new Regex(regexString);
        var res = rg.Match(value);
        return res.Success;
    }

    internal static bool BeAValidEligibilityCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var regexString = @"^\d{11}$";
        var rg = new Regex(regexString);
        var res = rg.Match(value);
        return res.Success;
    }

    internal static bool BeAValidDate(string value)
    {
        return DateTime.TryParseExact(value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var temp);
    }

    internal static bool BeAValidName(string value)
    {
        var regexString =
            @"^[a-zA-Z ,.'-]+$";
        var rg = new Regex(regexString);
        var res = rg.Match(value);
        return res.Success;
    }

    internal static bool BeAPastDate(DateTime value)
    {
        return value.CompareTo(DateTime.UtcNow) <= 0;
    }
}