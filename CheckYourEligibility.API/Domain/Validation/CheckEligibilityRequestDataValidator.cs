// Ignore Spelling: Validator

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

namespace FeatureManagement.Domain.Validation;

public class CheckEligibilityRequestDataValidator : AbstractValidator<IEligibilityServiceType>
{
    public CheckEligibilityRequestDataValidator()
    {
        // Rules for FSM, EYPP, 2YO
        When(x => x is CheckEligibilityRequestData, () =>
        {
            RuleFor(x => ((CheckEligibilityRequestData)x).LastName)
                .Must(DataValidation.BeAValidName)
                .WithMessage(ValidationMessages.LastName);

            RuleFor(x => ((CheckEligibilityRequestData)x).DateOfBirth)
                .NotEmpty()
                .Must(DataValidation.BeAValidDate)
                .WithMessage(ValidationMessages.DOB);

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).NationalInsuranceNumber), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).NationalAsylumSeekerServiceNumber)
                    .Empty()
                    .WithMessage(ValidationMessages.NI_and_NASS);
                RuleFor(x => ((CheckEligibilityRequestData)x).NationalInsuranceNumber)
                    .Must(DataValidation.BeAValidNi)
                    .WithMessage(ValidationMessages.NI);
            }).Otherwise(() =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).NationalAsylumSeekerServiceNumber)
                    .NotEmpty()
                    .WithMessage(ValidationMessages.NI_or_NASS);
            });
        });

        // Rules for Working families
        When(x => x is CheckEligibilityRequestWorkingFamiliesData, () =>
        {
            RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).EligibilityCode)
                .NotEmpty()
                .Must(DataValidation.BeAValidEligibilityCode)
                .WithMessage(ValidationMessages.EligibilityCode);
            RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).ChildDateOfBirth)
                .NotEmpty()
                .Must(DataValidation.BeAValidDate)
                .WithMessage(ValidationMessages.ChildDOB);
            RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).NationalInsuranceNumber)
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);
        });
    }
}