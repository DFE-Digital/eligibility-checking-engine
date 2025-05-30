// Ignore Spelling: Validator

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

namespace FeatureManagement.Domain.Validation;

public class CheckEligibilityRequestDataValidator_Eypp : AbstractValidator<CheckEligibilityRequestData_Eypp>
{
    public CheckEligibilityRequestDataValidator_Eypp()
    {
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(ValidationMessages.LastName);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAValidDate)
            .WithMessage(ValidationMessages.DOB);

        When(x => !string.IsNullOrEmpty(x.NationalInsuranceNumber), () =>
        {
            RuleFor(x => x.NationalInsuranceNumber)
                .NotEmpty()
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);
        });
    }
}