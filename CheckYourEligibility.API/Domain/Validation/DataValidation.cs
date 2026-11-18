using System.Globalization;
using System.Text.RegularExpressions;

namespace CheckYourEligibility.API.Domain.Validation;

internal static class DataValidation
{
    internal static bool BeAValidNi(string? value)
    {
        return NinoValidation.IsValidInput(value);
    }

    internal static bool BeAValidNass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        return Regex.IsMatch(value, @"\A(?:\d{9}|\d{2}/\d{2}/\d{5})\z");
    }

    internal static bool BeElevenDigitEligibilityCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!value.Trim().Length.Equals(11)) return false;
        return true;
    }

    internal static bool BeAValidEligibilityCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var regexString = @"\A\d{11}\z";
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
            @"\A[a-zA-Z" +
            @"ÁáÉéÍíÓóÚúÝýĆćĹĺŃńŔŕŚśŹź" +
            @"ÀàÈèÌìÒòÙùẀẁỲỳ" +
            @"ÂâÊêÎîÔôÛûĈĉĜĝĤĥĴĵŜŝŴŵŶŷ" +
            @"ÃãÑñÕõĨĩŨũẼẽỸỹ" +
            @"ÄäËëÏïÖöÜüŸÿ" +
            @"ÇçĢģĶķĻļŅņŖŗŞşŢţ" +
            @"ÅåŮů" +
            @"ĀāĒēĪīŌōŪūȲȳ" +
            @"ĂăĔĕĞğĬĭŎŏŬŭ" +
            @"ĊċĖėĠġİẊẋŻż" +
            @"ĄąĘęĮįŲų" +
            @"ŐőŰű" +
            @" ,.''\u2018\u2019-]+\z";

        var rg = new Regex(regexString);
        var res = rg.Match(value);
        return res.Success;
    }

    internal static bool BeAPastDate(DateTime value)
    {
        return value.CompareTo(DateTime.UtcNow) <= 0;
    }
	internal static bool BeAPastDate(string value)
	{
		DateTime valueDT = DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
		return valueDT.CompareTo(DateTime.UtcNow) <= 0;
	}

	internal static bool BeAValidPostcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
 
        var regexString =
            @"^(GIR\s?0AA|(?:[A-PR-UWYZ][0-9][0-9]?|[A-PR-UWYZ][A-HK-Y][0-9][0-9]?|[A-PR-UWYZ][0-9][A-HJKPSTUW]|[A-PR-UWYZ][A-HK-Y][0-9][ABEHMNPRVWXY])\s?[0-9][ABD-HJLNP-UW-Z]{2})\z";
        var rg = new Regex(regexString);
        var res = rg.Match(value.ToUpperInvariant());
        return res.Success;
    }
}
