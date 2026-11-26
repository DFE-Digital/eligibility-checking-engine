using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using FluentAssertions;

namespace CheckYourEligibility.API.Tests.Validators;

[TestFixture]
public class FosterFamilyValidatorTests
{
    [TestCase("Élodie")]
    [TestCase("O’Connor")]
    [TestCase("François")]
    [TestCase("Smith-Jones")]
    public void Carer_names_accept_supported_characters(string name)
    {
        var request = ValidCarer();
        request.CarerFirstName = name;
        request.CarerLastName = name;

        new FosterCarerRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase("Sam123")]
    [TestCase("Alex<script>")]
    public void Carer_names_reject_invalid_characters(string name)
    {
        var request = ValidCarer();
        request.CarerFirstName = name;
        request.CarerLastName = name;

        var result = new FosterCarerRequestValidator().Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "CarerFirstName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.CarerFirstNameInvalid);
        result.Errors.Should().Contain(e =>
            e.PropertyName == "CarerLastName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.CarerLastNameInvalid);
    }

    [Test]
    public void Missing_carer_names_use_foster_family_messages()
    {
        var result = new FosterCarerRequestValidator().Validate(new FosterCarerRequest());

        result.Errors.Should().Contain(e =>
            e.PropertyName == "CarerFirstName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.CarerFirstNameEmpty);
        result.Errors.Should().Contain(e =>
            e.PropertyName == "CarerLastName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.CarerLastNameEmpty);
    }

    [Test]
    public void Missing_carer_NINO_uses_the_foster_family_message()
    {
        var request = ValidCarer();
        request.CarerNationalInsuranceNumber = null;

        var result = new FosterCarerRequestValidator().Validate(request);

        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "CarerNationalInsuranceNumber" &&
            e.ErrorMessage == FosterFamilyValidationMessages.CarerNationalInsuranceNumberEmpty);
    }

    [TestCase("AB123456C")]
    [TestCase("ab 12 34 56 c")]
    [TestCase("AB-12-34-56-C")]
    public void Partner_accepts_valid_NINOs(string nino)
    {
        var request = ValidPartner();
        request.PartnerNationalInsuranceNumber = nino;

        new FosterPartnerRequestValidator().Validate(request).IsValid.Should().BeTrue();
        request.PartnerNationalInsuranceNumber.Should().Be(nino);
    }

    [TestCase("BG123456A")]
    [TestCase("AB123456E")]
    [TestCase("!!!")]
    public void Partner_rejects_invalid_NINOs(string nino)
    {
        var request = ValidPartner();
        request.PartnerNationalInsuranceNumber = nino;

        var result = new FosterPartnerRequestValidator().Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "PartnerNationalInsuranceNumber" &&
            e.ErrorMessage == ValidationMessages.InvalidNI);
    }

    [Test]
    public void Missing_partner_name_and_NINO_use_foster_family_messages()
    {
        var result = new FosterPartnerRequestValidator().Validate(new FosterPartnerRequest());

        result.Errors.Should().Contain(e =>
            e.PropertyName == "PartnerFirstName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.PartnerFirstNameEmpty);
        result.Errors.Should().Contain(e =>
            e.PropertyName == "PartnerLastName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.PartnerLastNameEmpty);
        result.Errors.Should().Contain(e =>
            e.PropertyName == "PartnerNationalInsuranceNumber" &&
            e.ErrorMessage == FosterFamilyValidationMessages.PartnerNationalInsuranceNumberEmpty);
    }

    [Test]
    public void Family_request_requires_partner_details_when_partner_selected()
    {
        var request = ValidFamily();
        request.HasPartner = true;
        request.Partner = null;

        var result = new FosterFamilyRequestValidator().Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "Partner" &&
            e.ErrorMessage == "Partner details are required.");
    }

    [Test]
    public void Family_request_does_not_validate_partner_when_partner_not_selected()
    {
        var request = ValidFamily();
        request.HasPartner = false;
        request.Partner = new FosterPartnerRequest();

        new FosterFamilyRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase("Élodie")]
    [TestCase("O’Connor")]
    [TestCase("Smith-Jones")]
    public void Child_names_accept_supported_characters(string name)
    {
        var request = ValidChild();
        request.ChildFirstName = name;
        request.ChildLastName = name;

        new FosterChildRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase("Sam123")]
    [TestCase("Alex<script>")]
    public void Child_names_reject_invalid_characters(string name)
    {
        var request = ValidChild();
        request.ChildFirstName = name;
        request.ChildLastName = name;

        var result = new FosterChildRequestValidator().Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "ChildFirstName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.ChildFirstNameInvalid);
        result.Errors.Should().Contain(e =>
            e.PropertyName == "ChildLastName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.ChildLastNameInvalid);
    }

    [Test]
    public void Missing_child_names_use_foster_family_messages()
    {
        var request = new FosterChildRequest
        {
            ChildFirstName = null!,
            ChildLastName = null!,
            ChildDateOfBirth = DateTime.Today.AddYears(-2),
            ChildPostCode = "SW1A 1AA"
        };

        var result = new FosterChildRequestValidator().Validate(request);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "ChildFirstName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.ChildFirstNameEmpty);
        result.Errors.Should().Contain(e =>
            e.PropertyName == "ChildLastName" &&
            e.ErrorMessage == FosterFamilyValidationMessages.ChildLastNameEmpty);
    }

    [TestCase("SW1A 1AA", true)]
    [TestCase("sw1a 1aa", true)]
    [TestCase("GIR 0AA", true)]
    [TestCase("XSW1A 1AA", false)]
    [TestCase("GIR 0AA!", false)]
    [TestCase("SW1A 1AA\n", false)]
    [TestCase(null, false)]
    public void Child_postcode_requires_a_complete_valid_match(string? postcode, bool expectedValid)
    {
        var request = ValidChild();
        request.ChildPostCode = postcode!;

        var result = new FosterChildRequestValidator().Validate(request);

        result.IsValid.Should().Be(expectedValid);
        if (!expectedValid)
        {
            result.Errors.Should().Contain(e =>
                e.PropertyName == "ChildPostCode" &&
                e.ErrorMessage == FosterFamilyValidationMessages.ChildPostCodeInvalid);
        }
    }

    private static FosterCarerRequest ValidCarer() => new()
    {
        CarerFirstName = "Joe",
        CarerLastName = "Bloggs",
        CarerDateOfBirth = DateTime.Today.AddYears(-40),
        CarerNationalInsuranceNumber = "AB123456C"
    };

    private static FosterPartnerRequest ValidPartner() => new()
    {
        PartnerFirstName = "Jane",
        PartnerLastName = "Bloggs",
        PartnerDateOfBirth = DateTime.Today.AddYears(-40),
        PartnerNationalInsuranceNumber = "CE123456A"
    };

    private static FosterChildRequest ValidChild() => new()
    {
        ChildFirstName = "Sam",
        ChildLastName = "Smith",
        ChildDateOfBirth = DateTime.Today.AddYears(-2),
        ChildPostCode = "SW1A 1AA"
    };

    private static FosterFamilyRequest ValidFamily() => new()
    {
        FosterCarer = ValidCarer(),
        FosterChild = ValidChild()
    };
}
