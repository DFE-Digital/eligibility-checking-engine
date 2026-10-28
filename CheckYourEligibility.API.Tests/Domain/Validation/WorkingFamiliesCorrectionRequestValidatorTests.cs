using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Validation;
using FluentAssertions;

namespace CheckYourEligibility.API.Tests.Validation;

[TestFixture]
public class WorkingFamiliesCorrectionRequestValidatorTests
{
    private readonly WorkingFamiliesChildCorrectionRequestValidator
        _childValidator = new();

    private readonly WorkingFamiliesAdultCorrectionRequestValidator
        _adultValidator = new();

    [TestCase("Élodie", "O’Connor")]
    [TestCase("Zoë", "Smith-Jones")]
    [TestCase("François", "D’Arcy")]
    public void Accented_names_and_supported_punctuation_are_accepted(
        string firstName, string lastName)
    {
        var child = ValidChild();
        child.FirstName = firstName;
        child.LastName = lastName;

        var adult = ValidAdult();
        adult.FirstName = firstName;
        adult.LastName = lastName;

        _childValidator.Validate(child).IsValid.Should().BeTrue();
        _adultValidator.Validate(adult).IsValid.Should().BeTrue();
    }

    [TestCase("Sam123")]
    [TestCase("Alex<script>")]
    public void Invalid_name_characters_are_rejected(string name)
    {
        var child = ValidChild();
        child.FirstName = name;
        child.LastName = name;

        var adult = ValidAdult();
        adult.FirstName = name;
        adult.LastName = name;

        _childValidator.Validate(child).Errors
            .Select(x => x.PropertyName)
            .Should().BeEquivalentTo(new[] { "FirstName", "LastName" });

        _adultValidator.Validate(adult).Errors
            .Select(x => x.PropertyName)
            .Should().BeEquivalentTo(new[] { "FirstName", "LastName" });
    }

    [TestCase(100, true)]
    [TestCase(101, false)]
    public void Name_length_limit_is_enforced(int length, bool expectedValid)
    {
        var child = ValidChild();
        child.FirstName = child.LastName = new string('A', length);

        var adult = ValidAdult();
        adult.FirstName = adult.LastName = new string('A', length);

        _childValidator.Validate(child).IsValid.Should().Be(expectedValid);
        _adultValidator.Validate(adult).IsValid.Should().Be(expectedValid);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-date")]
    [TestCase("2023-02-29")]
    [TestCase("15/06/2021")]
    public void Invalid_dates_return_validation_errors_without_throwing(
        string? date)
    {
        var child = ValidChild();
        child.DateOfBirth = date;

        var adult = ValidAdult();
        adult.DateOfBirth = date;

        _childValidator.Validate(child).Errors
            .Should().ContainSingle()
            .Which.PropertyName.Should().Be("DateOfBirth");

        _adultValidator.Validate(adult).Errors
            .Should().ContainSingle()
            .Which.PropertyName.Should().Be("DateOfBirth");
    }

    [Test]
    public void Future_dates_of_birth_are_rejected()
    {
        var future = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");

        var child = ValidChild();
        child.DateOfBirth = future;

        var adult = ValidAdult();
        adult.DateOfBirth = future;

        _childValidator.Validate(child).Errors
            .Should().ContainSingle()
            .Which.PropertyName.Should().Be("DateOfBirth");

        _adultValidator.Validate(adult).Errors
            .Should().ContainSingle()
            .Which.PropertyName.Should().Be("DateOfBirth");
    }

    [Test]
    public void Today_is_accepted_as_a_date_of_birth()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var child = ValidChild();
        child.DateOfBirth = today;

        var adult = ValidAdult();
        adult.DateOfBirth = today;

        _childValidator.Validate(child).IsValid.Should().BeTrue();
        _adultValidator.Validate(adult).IsValid.Should().BeTrue();
    }

    [TestCase("1900-01-01")]
    [TestCase("2000-01-01")]
    public void Child_corrections_do_not_apply_an_upper_age_limit(string date)
    {
        var child = ValidChild();
        child.DateOfBirth = date;

        _childValidator.Validate(child).IsValid.Should().BeTrue();
    }

    [TestCase("SW1A 1AA", true)]
    [TestCase("sw1a 1aa", true)]
    [TestCase("GIR 0AA", true)]
    [TestCase("SW1A1AA", false)]
    [TestCase("XSW1A 1AA", false)]
    [TestCase("GIR 0AA!", false)]
    [TestCase("SW1A 1AA\n", false)]
    public void Postcode_requires_a_complete_valid_match(
        string postcode, bool expectedValid)
    {
        var child = ValidChild();
        child.Postcode = postcode;

        _childValidator.Validate(child).IsValid.Should().Be(expectedValid);
    }

    [TestCase("AB123456C", true)]
    [TestCase("ab 12 34 56 c", true)]
    [TestCase("AB-12-34-56-C", true)]
    [TestCase("BG123456A", false)]
    [TestCase("AB123456E", false)]
    [TestCase("!!!", false)]
    public void Nino_uses_shared_validation_without_mutating_input(
        string nino, bool expectedValid)
    {
        var adult = ValidAdult();
        adult.NationalInsuranceNumber = nino;

        _adultValidator.Validate(adult).IsValid.Should().Be(expectedValid);
        adult.NationalInsuranceNumber.Should().Be(nino);
    }

    [Test]
    public void Missing_fields_return_one_error_per_field()
    {
        _childValidator.Validate(
                new WorkingFamiliesChildCorrectionRequest())
            .Errors.Select(x => x.PropertyName)
            .Should().BeEquivalentTo(new[]
            {
                "FirstName", "LastName", "DateOfBirth", "Postcode", "ExpectedVersion"
            });

        _adultValidator.Validate(
                new WorkingFamiliesAdultCorrectionRequest())
            .Errors.Select(x => x.PropertyName)
            .Should().BeEquivalentTo(new[]
            {
                "FirstName", "LastName", "DateOfBirth",
                "NationalInsuranceNumber", "ExpectedVersion"
            });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Missing_record_version_is_rejected(string? version)
    {
        var child = ValidChild();
        child.ExpectedVersion = version;

        var adult = ValidAdult();
        adult.ExpectedVersion = version;

        _childValidator.Validate(child).Errors
            .Should().ContainSingle()
            .Which.PropertyName.Should().Be("ExpectedVersion");

        _adultValidator.Validate(adult).Errors
            .Should().ContainSingle()
            .Which.PropertyName.Should().Be("ExpectedVersion");
    }

    private static WorkingFamiliesChildCorrectionRequest ValidChild()
    {
        return new WorkingFamiliesChildCorrectionRequest
        {
            FirstName = "Sam",
            LastName = "Child",
            DateOfBirth = "2021-06-15",
            Postcode = "SW1A 1AA",
            ExpectedVersion = $"v1.{new string('A', 64)}"
        };
    }

    private static WorkingFamiliesAdultCorrectionRequest ValidAdult()
    {
        return new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Alex",
            LastName = "Carer",
            DateOfBirth = "1985-05-10",
            NationalInsuranceNumber = "AB123456C",
            ExpectedVersion = $"v1.{new string('A', 64)}"
        };
    }
}