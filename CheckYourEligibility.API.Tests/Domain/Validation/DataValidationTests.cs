using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using FeatureManagement.Domain.Validation;
using FluentAssertions;

namespace CheckYourEligibility.API.Tests.Validators;

[TestFixture]
public class DataValidationTests
{
    private CheckEligibilityRequestDataValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new CheckEligibilityRequestDataValidator();
    }

    private CheckEligibilityRequestData ValidRequestWith(string lastName) =>
        new CheckEligibilityRequestData
        {
            LastName = lastName,
            DateOfBirth = "2000-01-01",
            NationalInsuranceNumber = "AB123456C",
            Type = CheckEligibilityType.FreeSchoolMeals
        };

    [TestCase("OBrien",       "plain letters")]
    [TestCase("O'Brien",      "straight apostrophe (U+0027)")]
    [TestCase("O\u2019Brien", "right curly apostrophe (U+2019)")]
    [TestCase("O\u2018Brien", "left curly apostrophe (U+2018)")]
    [TestCase("Smith-Jones",  "hyphen")]
    [TestCase("St. Claire",   "period and space")]
    [TestCase("van den Berg", "spaces")]
    public void LastName_with_valid_characters_passes_validation(string lastName, string reason)
    {
        var result = _validator.Validate(ValidRequestWith(lastName));

        result.Errors.Should().NotContain(
            e => e.PropertyName.Contains("LastName") && e.ErrorMessage.Contains("LastName"),
            because: reason);
    }

    [TestCase("ÁáÉéÍíÓóÚúÝýĆćĹĺŃńŔŕŚśŹź", "acute")]
    [TestCase("ÀàÈèÌìÒòÙùẀẁỲỳ", "grave")]
    [TestCase("ÂâÊêÎîÔôÛûĈĉĜĝĤĥĴĵŜŝŴŵŶŷ", "circumflex")]
    [TestCase("ÃãÑñÕõĨĩŨũẼẽỸỹ", "tilde")]
    [TestCase("ÄäËëÏïÖöÜüŸÿ", "umlaut or diaeresis")]
    [TestCase("ÇçĢģĶķĻļŅņŖŗŞşŢţ", "cedilla")]
    [TestCase("ÅåŮů", "ring")]
    [TestCase("ĀāĒēĪīŌōŪūȲȳ", "macron")]
    [TestCase("ĂăĔĕĞğĬĭŎŏŬŭ", "breve")]
    [TestCase("ĊċĖėĠġİẊẋŻż", "dot above")]
    [TestCase("ĄąĘęĮįŲų", "ogonek")]
    [TestCase("ŐőŰű", "double acute")]
    public void LastName_with_accented_characters_passes_validation(
    string lastName,
    string accentType)
    {
        var result = _validator.Validate(ValidRequestWith(lastName));

        result.Errors.Should().NotContain(
            e => e.PropertyName.Contains("LastName"),
            because: $"{accentType} accented characters are supported");
    }

    [TestCase("Smith123",   "digits not allowed")]
    [TestCase("O/Brien",    "forward slash not allowed")]
    [TestCase("Smith@Jones","at-sign not allowed")]
    public void LastName_with_invalid_characters_fails_validation(string lastName, string reason)
    {
        var result = _validator.Validate(ValidRequestWith(lastName));

        result.IsValid.Should().BeFalse(because: reason);
        result.Errors.Should().Contain(
            e => e.PropertyName.Contains("LastName"),
            because: reason);
    }

    private CheckEligibilityRequestData ValidRequestWithOptionalApplicationData() =>
    new CheckEligibilityRequestData
    {
        LastName = "Tester",
        FirstName = "Alex",
        DateOfBirth = "2000-01-01",
        ChildFirstName = "Sam",
        ChildLastName = "Tester",
        ChildDateOfBirth = "2016-04-12",
        ChildSchoolURN = "123456",
        NationalInsuranceNumber = "AB123456C",
        Type = CheckEligibilityType.FreeSchoolMeals
    };

    [TestCase("FirstName", "José")]
    [TestCase("LastName", "González")]
    [TestCase("ChildFirstName", "Dénis")]
    [TestCase("ChildLastName", "Müller")]
    public void Parent_and_child_name_fields_with_accented_characters_pass_validation(
    string fieldName,
    string value)
    {
        var request = ValidRequestWithOptionalApplicationData();

        switch (fieldName)
        {
            case nameof(request.FirstName):
                request.FirstName = value;
                break;
            case nameof(request.LastName):
                request.LastName = value;
                break;
            case nameof(request.ChildFirstName):
                request.ChildFirstName = value;
                break;
            case nameof(request.ChildLastName):
                request.ChildLastName = value;
                break;
            default:
                Assert.Fail($"Unsupported name field: {fieldName}");
                break;
        }

        var result = _validator.Validate(request);

        result.Errors.Should().NotContain(
            e => e.PropertyName.Contains(fieldName),
            because: $"{fieldName} should allow accented characters");
    }

    [Test]
    public void Optional_application_fields_with_valid_values_pass_validation()
    {
        var result = _validator.Validate(ValidRequestWithOptionalApplicationData());

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ChildDateOfBirth_with_invalid_value_fails_validation()
    {
        var request = ValidRequestWithOptionalApplicationData();
        request.ChildDateOfBirth = "not-a-date";

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ChildDateOfBirth"));
    }

    [Test]
    public void ChildSchoolURN_with_non_numeric_value_fails_validation()
    {
        var request = ValidRequestWithOptionalApplicationData();
        request.ChildSchoolURN = "ABC123";

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ChildSchoolURN"));
    }

    [Test]
    public void ChildFirstName_with_invalid_characters_fails_validation()
    {
        var request = ValidRequestWithOptionalApplicationData();
        request.ChildFirstName = "Sam#";

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ChildFirstName"));
    }

    [TestCase(CheckEligibilityType.FreeSchoolMeals, "123456789", true)]
    [TestCase(CheckEligibilityType.FreeSchoolMeals, "12345678", false)]
    [TestCase(CheckEligibilityType.FreeSchoolMeals, "1234567890", false)]
    [TestCase(CheckEligibilityType.FreeSchoolMeals, "12/34/56789", false)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium, "123456789", true)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium, "12345678", false)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium, "1234567890", false)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium, "12/34/56789", false)]
    public void Asylum_seeker_reference_number_validation_is_unchanged_for_fsm_and_eypp(
        CheckEligibilityType type,
        string referenceNumber,
        bool expectedIsValid)
    {
        var request = ValidRequestWith("Smith");
        request.NationalInsuranceNumber = null;
        request.AsylumSeekerReferenceNumber = referenceNumber;
        request.Type = type;

        var result = _validator.Validate(request);

        result.IsValid.Should().Be(expectedIsValid);
    }

    [TestCase(CheckEligibilityType.FreeSchoolMeals)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium)]
    public void Nino_validation_is_unchanged_for_fsm_and_eypp(CheckEligibilityType type)
    {
        var request = ValidRequestWith("Smith");
        request.NationalInsuranceNumber = "invalid";
        request.Type = type;

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(request.NationalInsuranceNumber) &&
            e.ErrorMessage == CheckYourEligibility.API.Domain.Constants.ErrorMessages.ValidationMessages.NI);
    }

    [TestCase(CheckEligibilityType.FreeSchoolMeals)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium)]
    public void Fsm_and_eypp_still_reject_a_request_containing_both_identifiers(CheckEligibilityType type)
    {
        var request = ValidRequestWith("Smith");
        request.AsylumSeekerReferenceNumber = "123456789";
        request.Type = type;

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(request.AsylumSeekerReferenceNumber) &&
            e.ErrorMessage == CheckYourEligibility.API.Domain.Constants.ErrorMessages.ValidationMessages.NI_and_ASRN);
    }

    [Test]
    public void Two_year_offer_requires_a_nino()
    {
        var request = ValidRequestWith("Smith");
        request.NationalInsuranceNumber = null;
        request.Type = CheckEligibilityType.TwoYearOffer;

        var result = _validator.Validate(request);

        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(request.NationalInsuranceNumber) &&
            e.ErrorMessage == CheckYourEligibility.API.Domain.Constants.ErrorMessages.ValidationMessages.NI_REQUIRED);
    }

    [Test]
    public void Two_year_offer_does_not_accept_nass_instead_of_nino()
    {
        var request = ValidRequestWith("Smith");
        request.NationalInsuranceNumber = null;
        request.AsylumSeekerReferenceNumber = "123456789";
        request.Type = CheckEligibilityType.TwoYearOffer;

        var result = _validator.Validate(request);

        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(request.NationalInsuranceNumber) &&
            e.ErrorMessage == CheckYourEligibility.API.Domain.Constants.ErrorMessages.ValidationMessages.NI_REQUIRED);
        result.Errors.Should().NotContain(e =>
            e.PropertyName == nameof(request.AsylumSeekerReferenceNumber));
    }

    [Test]
    public void Two_year_offer_validates_nino_and_ignores_nass()
    {
        var request = ValidRequestWith("Smith");
        request.AsylumSeekerReferenceNumber = "not-a-valid-nass";
        request.Type = CheckEligibilityType.TwoYearOffer;

        var result = _validator.Validate(request);

        result.Errors.Should().NotContain(e =>
            e.PropertyName == nameof(request.AsylumSeekerReferenceNumber));
        result.Errors.Should().NotContain(e =>
            e.PropertyName == nameof(request.NationalInsuranceNumber));
    }

    [Test]
    public void Two_year_offer_rejects_invalid_nino()
    {
        var request = ValidRequestWith("Smith");
        request.NationalInsuranceNumber = "invalid";
        request.Type = CheckEligibilityType.TwoYearOffer;

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(request.NationalInsuranceNumber) &&
            e.ErrorMessage == CheckYourEligibility.API.Domain.Constants.ErrorMessages.ValidationMessages.NI);
    }
}
