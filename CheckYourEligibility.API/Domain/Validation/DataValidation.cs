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
        var regexString = @"^\d{9}$";
        var rg = new Regex(regexString);
        var res = rg.Match(value);
        return res.Success;
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
            @"^[a-zA-Z" +
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
            @" ,.''\u2018\u2019-]+$";

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

        var regexString = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([A-Za-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9]?[A-Za-z])))) [0-9][A-Za-z]{2})$";
        var rg = new Regex(@"\A(?:" + regexString + @")\z");
        var res = rg.Match(value);
        return res.Success;
    }
}