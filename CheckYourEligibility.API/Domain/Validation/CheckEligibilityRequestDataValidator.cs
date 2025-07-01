// Ignore Spelling: Validator

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

namespace FeatureManagement.Domain.Validation;

public class CheckEligibilityRequestDataValidator : AbstractValidator<CheckEligibilityRequestData>
{
    public CheckEligibilityRequestDataValidator()
    {

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAValidDate)
            .WithMessage(ValidationMessages.DOB);

        When(x => x.Type == CheckEligibilityType.WorkingFamilies, () =>
        {
            RuleFor(x => x.NationalInsuranceNumber)
                .NotEmpty()
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);
            RuleFor(x => x.EligibilityCode)
                .NotEmpty()
                .When(x => x.Type == CheckEligibilityType.WorkingFamilies)
                .Must(DataValidation.BeValidEligibilityCode)
                .WithMessage(ValidationMessages.EligibilityCode);
        }).Otherwise(() =>
        {
            RuleFor(x => x.LastName)
           .Must(DataValidation.BeAValidName)
           .WithMessage(ValidationMessages.LastName);

            When(x => !string.IsNullOrEmpty(x.NationalInsuranceNumber), () =>
            {
                RuleFor(x => x.NationalAsylumSeekerServiceNumber)
                    .Empty()
                    .WithMessage(ValidationMessages.NI_and_NASS);
                RuleFor(x => x.NationalInsuranceNumber)
                    .Must(DataValidation.BeAValidNi)
                    .WithMessage(ValidationMessages.NI);
            }).Otherwise(() =>
            {
                RuleFor(x => x.NationalAsylumSeekerServiceNumber)
                    .NotEmpty()
                    .WithMessage(ValidationMessages.NI_or_NASS);
            });
        });

    }
}