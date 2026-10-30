using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using FluentValidation;

namespace CheckYourEligibility.API.Domain.Validation;

public class WorkingFamiliesAdultCorrectionRequestValidator
    : AbstractValidator<WorkingFamiliesAdultCorrectionRequest>
{
    public WorkingFamiliesAdultCorrectionRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Enter a first name")
            .MaximumLength(100)
            .WithMessage("First name must be 100 characters or fewer")
            .Must(value => DataValidation.BeAValidName(value!))
            .WithMessage("First name contains an invalid character");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Enter a last name")
            .MaximumLength(100)
            .WithMessage("Last name must be 100 characters or fewer")
            .Must(value => DataValidation.BeAValidName(value!))
            .WithMessage(ValidationMessages.LastNameWf);

        RuleFor(x => x.DateOfBirth)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Enter a date of birth")
            .Must(value => DataValidation.BeAValidDate(value!))
            .WithMessage(
                "Enter a valid date of birth in yyyy-MM-dd format")
            .Must(value => DataValidation.BeAPastDate(value!))
            .WithMessage("Date of birth must not be in the future");

        RuleFor(x => x.NationalInsuranceNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(ValidationMessages.ParentNINumberEmpty)
            .Must(value => DataValidation.BeAValidNi(value))
            .WithMessage(ValidationMessages.NI);

        RuleFor(x => x.ExpectedVersion)
            .NotEmpty()
            .WithMessage("A record version is required. Reload the record and try again.");
    }
}