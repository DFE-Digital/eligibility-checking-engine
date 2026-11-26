using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

public class FosterFamilyRequestValidator : AbstractValidator<FosterFamilyRequest>
{
    public FosterFamilyRequestValidator(bool enforceSubmissionDateWindow = true)
    {
        RuleFor(x => x.FosterCarer)
            .NotNull();

        RuleFor(x => x.FosterChild)
            .NotNull();

        RuleFor(x => x.FosterCarer!)
            .SetValidator(new FosterCarerRequestValidator());

        RuleFor(x => x.FosterChild!)
            .SetValidator(new FosterChildRequestValidator(enforceSubmissionDateWindow));

        When(x => x.HasPartner, () =>
        {
            RuleFor(x => x.Partner)
                .NotNull()
                .WithMessage("Partner details are required.");

            RuleFor(x => x.Partner!)
                .SetValidator(new FosterPartnerRequestValidator());
        });
    }
}

public class FosterCarerRequestValidator
    : AbstractValidator<FosterCarerRequest>
{
    public FosterCarerRequestValidator()
    {

        RuleFor(x => x.CarerFirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(FosterFamilyValidationMessages.CarerFirstNameEmpty)
            .Must(DataValidation.BeAValidName)
            .WithMessage(FosterFamilyValidationMessages.CarerFirstNameInvalid);

        RuleFor(x => x.CarerLastName)
               .Cascade(CascadeMode.Stop)
               .NotEmpty()
               .WithMessage(FosterFamilyValidationMessages.CarerLastNameEmpty)
               .Must(DataValidation.BeAValidName)
               .WithMessage(FosterFamilyValidationMessages.CarerLastNameInvalid);

        RuleFor(x => x.CarerDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.DOB);

        RuleFor(x => x.CarerNationalInsuranceNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(FosterFamilyValidationMessages.CarerNationalInsuranceNumberEmpty)
            .Must(DataValidation.BeAValidNi)
            .WithMessage(ValidationMessages.InvalidNI);
    }
}

public class FosterPartnerRequestValidator
    : AbstractValidator<FosterPartnerRequest>
{
    public FosterPartnerRequestValidator()
    {
        RuleFor(x => x.PartnerFirstName)
               .Cascade(CascadeMode.Stop)
               .NotEmpty()
               .WithMessage(FosterFamilyValidationMessages.PartnerFirstNameEmpty)
               .Must(DataValidation.BeAValidName)
               .WithMessage(FosterFamilyValidationMessages.PartnerFirstNameInvalid);

        RuleFor(x => x.PartnerLastName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(FosterFamilyValidationMessages.PartnerLastNameEmpty)
                .Must(DataValidation.BeAValidName)
                .WithMessage(FosterFamilyValidationMessages.PartnerLastNameInvalid);

        RuleFor(x => x.PartnerDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.DOB);

        RuleFor(x => x.PartnerNationalInsuranceNumber)
                 .Cascade(CascadeMode.Stop)
                 .NotEmpty()
                 .WithMessage(FosterFamilyValidationMessages.PartnerNationalInsuranceNumberEmpty)
                 .Must(DataValidation.BeAValidNi)
                 .WithMessage(ValidationMessages.InvalidNI);
    }
}

public class FosterChildRequestValidator
    : AbstractValidator<FosterChildRequest>
{
    public FosterChildRequestValidator(bool enforceSubmissionDateWindow = true)
    {
        RuleFor(x => x.ChildFirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(FosterFamilyValidationMessages.ChildFirstNameEmpty)
            .Must(DataValidation.BeAValidName)
            .WithMessage(FosterFamilyValidationMessages.ChildFirstNameInvalid);

        RuleFor(x => x.ChildLastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(FosterFamilyValidationMessages.ChildLastNameEmpty)
            .Must(DataValidation.BeAValidName)
            .WithMessage(FosterFamilyValidationMessages.ChildLastNameInvalid);

        RuleFor(x => x.ChildDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.ChildDOB);

        RuleFor(x => x.ChildPostCode)
              .Must(DataValidation.BeAValidPostcode)
              .WithMessage(FosterFamilyValidationMessages.ChildPostCodeInvalid);

        When(x => x.SubmissionDate.HasValue, () =>
        {
            RuleFor(x => x.SubmissionDate!.Value)
                .Must(DataValidation.BeAPastDate)
                .WithMessage(ValidationMessages.SubmissionDate);

            When(_ => enforceSubmissionDateWindow, () =>
            {
                RuleFor(x => x.SubmissionDate!.Value)
                    .Must(value => value >= DateTime.Today.AddDays(-31))
                    .WithMessage(ValidationMessages.SubmissionDateOutOfRange);
            });
        });
    }
}

public class FosterChildReconfirmRequestValidator
    : AbstractValidator<FosterChildReconfirmRequest>
{
    public FosterChildReconfirmRequestValidator()
    {
        RuleFor(x => x.EligibilityCode)
            .NotEmpty()
            .WithMessage(ValidationMessages.EligibilityCodeLength);

        RuleFor(x => x.SubmissionDate)
            .NotEmpty()
            .WithMessage(ValidationMessages.SubmissionDate);
    }
}
