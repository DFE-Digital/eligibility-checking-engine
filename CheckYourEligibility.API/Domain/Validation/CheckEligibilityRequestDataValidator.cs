// Ignore Spelling: Validator

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;

namespace FeatureManagement.Domain.Validation;

public class CheckEligibilityRequestDataValidator : AbstractValidator<IEligibilityServiceType>
{
    public CheckEligibilityRequestDataValidator()
    {
        When(x => x is CheckEligibilityRequestDataBase, () =>
        {
            RuleFor(x => ((CheckEligibilityRequestDataBase)x).Reference)
                .MaximumLength(100)
                .WithMessage(ValidationMessages.ReferenceTooLong);
        });

        // Rules for FSM, EYPP, 2YO
        When(x => x is CheckEligibilityRequestData, () =>
        {
            When(x => string.IsNullOrEmpty(((CheckEligibilityRequestData)x).LastName), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).LastName)
                    .NotEmpty()
                    .WithMessage(ValidationMessages.LastName);
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).LastName), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).LastName)
                    .Must(DataValidation.BeAValidName)
                    .WithMessage(ValidationMessages.LastName);
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).FirstName), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).FirstName)
                    .Must(DataValidation.BeAValidName)
                    .WithMessage(ValidationMessages.FirstName);
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).ChildFirstName), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).ChildFirstName)
                    .Must(DataValidation.BeAValidName)
                    .WithMessage(ValidationMessages.ChildFirstName);
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).ChildLastName), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).ChildLastName)
                    .Must(DataValidation.BeAValidName)
                    .WithMessage(ValidationMessages.ChildLastName);
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).ChildDateOfBirth), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).ChildDateOfBirth)
                    .Must(DataValidation.BeAValidDate)
                    .WithMessage(ValidationMessages.ChildDOB);
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).ChildSchoolURN), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).ChildSchoolURN)
                    .Must(x => int.TryParse(x, out var urn) && urn > 0)
                    .WithMessage("Invalid Child School URN");
            });

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).EmailAddress), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).EmailAddress)
                    .EmailAddress()
                    .WithMessage(ValidationMessages.Email);
            });

            RuleFor(x => ((CheckEligibilityRequestData)x).DateOfBirth)
                .NotEmpty()
                .Must(DataValidation.BeAValidDate)
                .WithMessage(ValidationMessages.DOB);

            When(x => ((CheckEligibilityRequestData)x).Type == CheckEligibilityType.TwoYearOffer, () =>
            {
                RuleFor(x => ((CheckEligibilityRequestData)x).NationalInsuranceNumber)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(ValidationMessages.NI_REQUIRED)
                    .Must(DataValidation.BeAValidNi)
                    .WithMessage(ValidationMessages.NI);
            }).Otherwise(() =>
            {
                When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).NationalInsuranceNumber), () =>
                {
                    RuleFor(x => ((CheckEligibilityRequestData)x).AsylumSeekerReferenceNumber)
                        .Empty()
                        .WithMessage(ValidationMessages.NI_and_ASRN);

                    RuleFor(x => ((CheckEligibilityRequestData)x).NationalInsuranceNumber)
                        .Must(DataValidation.BeAValidNi)
                        .WithMessage(ValidationMessages.NI);

                }).Otherwise(() =>
                {
                    When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestData)x).AsylumSeekerReferenceNumber), () =>
                    {
                        RuleFor(x => ((CheckEligibilityRequestData)x).AsylumSeekerReferenceNumber)
                            .Must(DataValidation.BeAValidNass)
                            .WithMessage(ValidationMessages.NASS);
                    });

                    RuleFor(x => ((CheckEligibilityRequestData)x).AsylumSeekerReferenceNumber)
                        .NotEmpty()
                        .WithMessage(ValidationMessages.NI_or_ASRN);
                });
            });
        });

        // Rules for Working families
        When(x => x is CheckEligibilityRequestWorkingFamiliesData, () =>
        {
            RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).EligibilityCode)
			.Cascade(CascadeMode.Stop)
			.NotEmpty()
				.WithMessage(ValidationMessages.EligibilityCodeEmpty)
			.Must(DataValidation.BeElevenDigitEligibilityCode)
				.WithMessage(ValidationMessages.EligibilityCodeLength)
			.Must(DataValidation.BeAValidEligibilityCode)
				.WithMessage(ValidationMessages.EligibilityCodeInvalid);

			RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).NationalInsuranceNumber)
                .Must(DataValidation.BeAValidNi)
                .WithMessage(ValidationMessages.NI);

            RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).DateOfBirth)
                .NotEmpty()
                .Must(DataValidation.BeAValidDate)
                .WithMessage(ValidationMessages.DOB);

            When(x => !string.IsNullOrEmpty(((CheckEligibilityRequestWorkingFamiliesData)x).LastName), () =>
            {
                RuleFor(x => ((CheckEligibilityRequestWorkingFamiliesData)x).LastName)
                    .Must(DataValidation.BeAValidName)
                    .WithMessage(ValidationMessages.LastNameWf);
            });
        });
    }
}