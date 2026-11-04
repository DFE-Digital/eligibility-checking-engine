using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using FluentValidation;

namespace CheckYourEligibility.API.Domain.Validation;

public class WorkingFamiliesChildCorrectionRequestValidator
    : AbstractValidator<WorkingFamiliesChildCorrectionRequest>
{
    public WorkingFamiliesChildCorrectionRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildFirstNameEmpty)
            .MaximumLength(100)
            .WithMessage("First name must be 100 characters or fewer")
            .Must(value => DataValidation.BeAValidName(value!))
            .WithMessage("First name contains an invalid character");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildLastNameEmpty)
            .MaximumLength(100)
            .WithMessage("Last name must be 100 characters or fewer")
            .Must(value => DataValidation.BeAValidName(value!))
            .WithMessage(ValidationMessages.LastNameWf);

        RuleFor(x => x.DateOfBirth)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildDOBEmpty)
            .Must(value => DataValidation.BeAValidDate(value!))
            .WithMessage(
                "Enter a valid date of birth in yyyy-MM-dd format")
            .Must(value => DataValidation.BeAPastDate(value!))
            .WithMessage("Date of birth must not be in the future");

        RuleFor(x => x.Postcode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildPostCodeEmpty)
            .MaximumLength(9)
            .WithMessage("Postcode must be 9 characters or fewer")
            .Must(value => DataValidation.BeAValidPostcode(value))
            .WithMessage(ValidationMessages.ChildPostCodeInvalid);

        RuleFor(x => x.ExpectedVersion)
            .NotEmpty()
            .WithMessage("A record version is required. Reload the record and try again.");
    }
}