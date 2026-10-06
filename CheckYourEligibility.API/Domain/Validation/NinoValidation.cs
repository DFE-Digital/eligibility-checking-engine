using System.Text.RegularExpressions;

namespace CheckYourEligibility.API.Domain.Validation;

public static class NinoValidation
{
    private static readonly Regex NonAsciiAlphaNumeric =
        new(@"[^a-zA-Z0-9]");

    private static readonly Regex CanonicalPattern =
        new(@"\A(?!BG|GB|NK|KN|TN|NT|ZZ)[A-CEGHJ-PR-TW-Z][A-CEGHJ-NPR-TW-Z][0-9]{6}[A-D]\z");

    public static string? Normalize(string? value)
    {
        return value is null
            ? null
            : NonAsciiAlphaNumeric.Replace(value, string.Empty)
                .ToUpperInvariant();
    }

    public static bool IsValidCanonical(string? value)
    {
        return value is not null && CanonicalPattern.IsMatch(value);
    }

    public static bool IsValidInput(string? value, bool required = true)
    {
        // Preserve existing optional-field treatment of blank input.
        // Check before cleaning so punctuation-only input cannot bypass validation.
        if (string.IsNullOrWhiteSpace(value))
        {
            return !required;
        }

        return IsValidCanonical(Normalize(value));
    }
}