using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Enums;
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
}
