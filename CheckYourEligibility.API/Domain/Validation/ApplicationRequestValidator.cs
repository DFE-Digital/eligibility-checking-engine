// Ignore Spelling: Validator

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

namespace FeatureManagement.Domain.Validation;

public class ApplicationRequestValidator : AbstractValidator<ApplicationRequest>
{
    public ApplicationRequestValidator()
    {
        RuleFor(x => x.Data)
            .NotNull()
            .WithMessage("data is required");

        RuleFor(x => x.Data!.ParentFirstName)
            .NotEmpty().WithMessage(ValidationMessages.FirstName);
        RuleFor(x => x.Data!.ParentLastName)
            .NotEmpty().WithMessage(ValidationMessages.LastName);
        RuleFor(x => x.Data!.ChildFirstName)
            .NotEmpty().WithMessage(ValidationMessages.ChildFirstName);
        RuleFor(x => x.Data!.ChildLastName)
            .NotEmpty().WithMessage(ValidationMessages.ChildLastName);

        RuleFor(x => x.Data!.ParentDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAValidDate)
            .WithMessage(ValidationMessages.DOB);
        RuleFor(x => x.Data!.ChildDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAValidDate)
            .WithMessage(ValidationMessages.ChildDOB);

        When(x => !string.IsNullOrEmpty(x.Data!.ParentNationalInsuranceNumber), () =>
        {
            RuleFor(x => x.Data!.ParentNationalAsylumSeekerServiceNumber)
                .Empty()
                .WithMessage(ValidationMessages.NI_and_NASS);
            RuleFor(x => x.Data!.ParentNationalInsuranceNumber)
                .NotEmpty()
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);
        }).Otherwise(() =>
        {
            RuleFor(x => x.Data!.ParentNationalAsylumSeekerServiceNumber)
                .NotEmpty()
                .WithMessage(ValidationMessages.NI_or_NASS);
        });
    }
}