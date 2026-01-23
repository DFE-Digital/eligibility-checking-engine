using System.Data;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

public class FosterFamilyRequestValidator : AbstractValidator<FosterFamilyRequest>
{
    public FosterFamilyRequestValidator()
    {
        RuleFor(x => x.Data)
            .NotNull()
            .WithMessage("data is required");

        //  Carer details validation
        RuleFor(x => x.Data.CarerFirstName)
            .NotEmpty().WithMessage(ValidationMessages.FirstName);
        RuleFor(x => x.Data.CarerLastName)
            .NotEmpty().WithMessage(ValidationMessages.LastName);

        RuleFor(x => x.Data.CarerDateOfBirth.ToString("yyyy-MM-dd"))
            .NotEmpty()
            .Must(DataValidation.BeAValidDate)
            .WithMessage(ValidationMessages.DOB);

        RuleFor(x => x.Data.CarerNationalInsuranceNumber)
            .NotEmpty()
            .Must(DataValidation.BeAValidNi)
            .WithMessage(ValidationMessages.NI);

        // Has Partner validation
        RuleFor(x => x.Data.HasPartner)
            .NotNull()
            .WithMessage("Has Partner is required");

        // Partner details validation

        When(x => x.Data.HasPartner == true, () =>
        {
            RuleFor(x => x.Data.PartnerFirstName)
                .NotEmpty()
                .WithMessage(ValidationMessages.FirstName);

            RuleFor(x => x.Data.PartnerLastName)
                .NotEmpty()
                .WithMessage(ValidationMessages.LastName);

            RuleFor(x => x.Data.PartnerDateOfBirth)
                .NotEmpty()
                .Must(dob => DataValidation.BeAValidDate(dob?.ToString("yyyy-MM-dd")))
                .WithMessage(ValidationMessages.DOB);

            RuleFor(x => x.Data.PartnerNationalInsuranceNumber)
                .NotEmpty()
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);
        });


        // Child details validation
        RuleFor(x => x.Data.ChildFirstName)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildFirstName);

        RuleFor(x => x.Data.ChildLastName)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChildLastName);

        RuleFor(x => x.Data.ChildDateOfBirth.ToString("yyyy-MM-dd"))
            .NotEmpty()
            .Must(DataValidation.BeAValidDate)
            .WithMessage(ValidationMessages.ChildDOB);

        RuleFor(x => x.Data.ChildPostCode)
            .NotEmpty()
            .WithMessage("Child PostCode is required");
    }
}