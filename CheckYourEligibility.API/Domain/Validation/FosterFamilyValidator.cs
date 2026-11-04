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
            .NotEmpty()
            .WithMessage(ValidationMessages.FirstName);

        RuleFor(x => x.CarerLastName)
            .NotEmpty()
            .WithMessage(ValidationMessages.LastName);

        RuleFor(x => x.CarerDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.DOB);
 
        RuleFor(x => x.CarerNationalInsuranceNumber)
            .NotEmpty()
            .Must(DataValidation.BeAValidNi)
            .WithMessage(ValidationMessages.NI);
    }
}

public class FosterPartnerRequestValidator
    : AbstractValidator<FosterPartnerRequest>
{
    public FosterPartnerRequestValidator()
    {
        RuleFor(x => x.PartnerFirstName)
            .NotEmpty()
            .WithMessage(ValidationMessages.FirstName);

        RuleFor(x => x.PartnerLastName)
            .NotEmpty()
            .WithMessage(ValidationMessages.LastName);

        RuleFor(x => x.PartnerDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.DOB);
 
        RuleFor(x => x.PartnerNationalInsuranceNumber)
            .NotEmpty()
            .Must(DataValidation.BeAValidNi)
            .WithMessage(ValidationMessages.NI);
    }
}

public class FosterChildRequestValidator
    : AbstractValidator<FosterChildRequest>
{
    public FosterChildRequestValidator(bool enforceSubmissionDateWindow = true)
    {
        RuleFor(x => x.ChildFirstName)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildFirstName);

        RuleFor(x => x.ChildLastName)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildLastName);

        RuleFor(x => x.ChildDateOfBirth)
            .NotEmpty()
            .Must(DataValidation.BeAPastDate)
            .WithMessage(ValidationMessages.ChildDOB);
 
        RuleFor(x => x.ChildPostCode)
            .NotEmpty()
            .WithMessage("Child PostCode is required");

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