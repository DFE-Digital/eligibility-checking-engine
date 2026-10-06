using CheckYourEligibility.API.Domain.Validation;

namespace CheckYourEligibility.API.Tests.Validation;

[TestFixture]
public class NinoValidationTests
{
    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase("ab123456c", "AB123456C")]
    [TestCase(" ab 12 34 56 c ", "AB123456C")]
    [TestCase("ab-12.34/56c", "AB123456C")]
    [TestCase("ab\t12\r\n3456c", "AB123456C")]
    [TestCase("---", "")]
    [TestCase("AB123456CD", "AB123456CD")]
    [TestCase("\u0131b123456c", "B123456C")]
    public void Normalize_ReturnsExpectedValue(
        string? input, string? expected)
    {
        Assert.That(NinoValidation.Normalize(input), Is.EqualTo(expected));
    }

    [TestCase("AB123456A")]
    [TestCase("AB123456B")]
    [TestCase("AB123456C")]
    [TestCase("AB123456D")]
    public void Canonical_AcceptsValidSuffixes(string value)
    {
        Assert.That(NinoValidation.IsValidCanonical(value), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ab123456c")]
    [TestCase("AB 123456C")]
    [TestCase("AB123456C\n")]
    [TestCase("AB123456")]
    [TestCase("AB123456 ")]
    [TestCase("AB123456E")]
    [TestCase("AB12345C")]
    [TestCase("AB1234567C")]
    [TestCase("AB123456CD")]
    [TestCase("A1123456C")]
    [TestCase("1B123456C")]
    [TestCase("DB123456C")]
    [TestCase("FB123456C")]
    [TestCase("IB123456C")]
    [TestCase("QB123456C")]
    [TestCase("UB123456C")]
    [TestCase("VB123456C")]
    [TestCase("AD123456C")]
    [TestCase("AF123456C")]
    [TestCase("AI123456C")]
    [TestCase("AO123456C")]
    [TestCase("AQ123456C")]
    [TestCase("AU123456C")]
    [TestCase("AV123456C")]
    [TestCase("BG123456C")]
    [TestCase("GB123456C")]
    [TestCase("NK123456C")]
    [TestCase("KN123456C")]
    [TestCase("TN123456C")]
    [TestCase("NT123456C")]
    [TestCase("ZZ123456C")]
    [TestCase("AB\u0661\u0662\u0663\u0664\u0665\u0666C")]
    public void Canonical_RejectsInvalidValues(string? value)
    {
        Assert.That(NinoValidation.IsValidCanonical(value), Is.False);
    }

    [TestCase("ab 12 34 56 c")]
    [TestCase("ab-12.34/56c")]
    [TestCase("ab\t12\r\n3456c")]
    public void Input_AcceptsAgreedFormatting(string value)
    {
        Assert.That(NinoValidation.IsValidInput(value), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankInput_DependsOnWhetherFieldIsRequired(string? value)
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                NinoValidation.IsValidInput(value, required: true),
                Is.False);
            Assert.That(
                NinoValidation.IsValidInput(value, required: false),
                Is.True);
        });
    }

    [TestCase("---")]
    [TestCase(" . / ")]
    [TestCase("AB123456CD")]
    [TestCase("BG123456C")]
    [TestCase("AB123456 ")]
    [TestCase("AB\u0661\u0662\u0663\u0664\u0665\u0666C")]
    public void InvalidSuppliedInput_FailsEvenWhenOptional(string value)
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                NinoValidation.IsValidInput(value, required: true),
                Is.False);
            Assert.That(
                NinoValidation.IsValidInput(value, required: false),
                Is.False);
        });
    }

    [Test]
    [SetCulture("tr-TR")]
    public void Normalize_IsIndependentOfCurrentCulture()
    {
        Assert.That(
            NinoValidation.Normalize("ib123456c"),
            Is.EqualTo("IB123456C"));
    }
}