// Ignore Spelling: Validator

using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;

namespace FeatureManagement.Domain.Validation;

public class WorkingFamiliesEventImportValidator : AbstractValidator<WorkingFamiliesEvent>
{
    public WorkingFamiliesEventImportValidator()
    {
        RuleFor(x => x.EligibilityCode)
            .NotEmpty()
            .Must(DataValidation.BeAValidEligibilityCode)
            .WithMessage(ValidationMessages.EligibilityCode);

        RuleFor(x => x.ParentFirstName)
            .NotEmpty().WithMessage("Parent " + ValidationMessages.FirstName);
        RuleFor(x => x.ParentLastName)
            .NotEmpty().WithMessage("Parent " + ValidationMessages.LastName);
        RuleFor(x => x.ChildFirstName)
            .NotEmpty().WithMessage("Child " + ValidationMessages.ChildFirstName);
        RuleFor(x => x.ChildLastName)
            .NotEmpty().WithMessage("Child " + ValidationMessages.ChildLastName);

        When(x => !string.IsNullOrEmpty(x.ParentNationalInsuranceNumber), () =>
        {
            RuleFor(x => x.ParentNationalInsuranceNumber)
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);
        });

        When(x => !x.PartnerNationalInsuranceNumber.IsNullOrEmpty(), () =>
        {
            RuleFor(x => x.PartnerFirstName)
                .NotEmpty().WithMessage("Partner " + ValidationMessages.FirstName);
            RuleFor(x => x.PartnerLastName)
                .NotEmpty().WithMessage("Partner " + ValidationMessages.LastName);
        });

        RuleFor(x => x.SubmissionDate)
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.SubmissionDate);
    }
}