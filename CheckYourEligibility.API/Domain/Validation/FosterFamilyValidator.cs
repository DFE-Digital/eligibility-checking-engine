using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

public class FosterFamilyRequestValidator : AbstractValidator<FosterFamilyRequest>
{
    public FosterFamilyRequestValidator()
    {
        RuleFor(x => x.FosterCarer)
            .NotNull();

        RuleFor(x => x.FosterChild)
            .NotNull();

        RuleFor(x => x.FosterCarer!)
            .SetValidator(new FosterCarerRequestValidator());

        RuleFor(x => x.FosterChild!)
            .SetValidator(new FosterChildRequestValidator());

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

internal class FosterCarerRequestValidator
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
            .WithMessage(ValidationMessages.DOB);

        RuleFor(x => x.CarerNationalInsuranceNumber)
            .NotEmpty()
            .Must(DataValidation.BeAValidNi)
            .WithMessage(ValidationMessages.NI);
    }
}

internal class FosterPartnerRequestValidator
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
            .WithMessage(ValidationMessages.DOB);

        RuleFor(x => x.PartnerNationalInsuranceNumber)
            .NotEmpty()
            .Must(DataValidation.BeAValidNi)
            .WithMessage(ValidationMessages.NI);
    }
}

internal class FosterChildRequestValidator
    : AbstractValidator<FosterChildRequest>
{
    public FosterChildRequestValidator()
    {
        RuleFor(x => x.ChildFirstName)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildFirstName);

        RuleFor(x => x.ChildLastName)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildLastName);

        RuleFor(x => x.ChildDateOfBirth)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildDOB);

        RuleFor(x => x.ChildPostCode)
            .NotEmpty()
            .WithMessage("Child PostCode is required");
    }
}