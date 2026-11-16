// Ignore Spelling: Validator

using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using static WorkingFamiliesEventHelper;

namespace FeatureManagement.Domain.Validation;

public class WorkingFamiliesEventImportValidator : AbstractValidator<WorkingFamiliesEvent>
{
	public WorkingFamiliesEventImportValidator()
	{
		//EligibilityCode
		RuleFor(x => x.EligibilityCode)
			.Cascade(CascadeMode.Stop)
			.NotEmpty()
				.WithMessage(ValidationMessages.EligibilityCodeEmpty)
			.Must(DataValidation.BeElevenDigitEligibilityCode)
				.WithMessage(ValidationMessages.EligibilityCodeLength)
			.Must(DataValidation.BeAValidEligibilityCode)
				.WithMessage(ValidationMessages.EligibilityCodeInvalid);

		//ValidityStartDate
		RuleFor(x => x.ValidityStartDate)
		.Cascade(CascadeMode.Stop)
			.Must(x => x != ImportDateValues.EmptyDate)
				.WithMessage(ValidationMessages.ValidityStartDateEmpty)
			.Must(x => x != ImportDateValues.InvalidDate)
				.WithMessage(ValidationMessages.ValidityStartDateInvalid);

		//ValidityEndDate
		RuleFor(x => x.ValidityEndDate)
		.Cascade(CascadeMode.Stop)
			.Must(x => x != ImportDateValues.EmptyDate)
				.WithMessage(ValidationMessages.ValidityEndDateEmpty)
			.Must(x => x != ImportDateValues.InvalidDate)
				.WithMessage(ValidationMessages.ValidityEndDateInvalid);

		//Parent
		RuleFor(x => x.ParentNationalInsuranceNumber)
			.Cascade(CascadeMode.Stop)
			.NotEmpty()
				.WithMessage(ValidationMessages.ParentNINumberEmpty)
			.Must(DataValidation.BeAValidNi)
				.WithMessage(ValidationMessages.ParentNINumberInvalid);

		RuleFor(x => x.ParentDateOfBirth)
			.Cascade(CascadeMode.Stop)
			.Must(x => x != ImportDateValues.EmptyDate)
				.WithMessage(ValidationMessages.ParentDOBEmpty)
			.Must(x => x != ImportDateValues.InvalidDate)
				.WithMessage(ValidationMessages.ParentDOBInvalid);

		RuleFor(x => x.ParentFirstName)
			.NotEmpty()
				.WithMessage(ValidationMessages.ParentFirstNameEmpty);

		RuleFor(x => x.ParentLastName)
			.NotEmpty()
				.WithMessage(ValidationMessages.ParentLastNameEmpty);

		//Child
		RuleFor(x => x.ChildFirstName)
			.NotEmpty()
				.WithMessage(ValidationMessages.ChildFirstNameEmpty);

		RuleFor(x => x.ChildLastName)
			.NotEmpty()
				.WithMessage(ValidationMessages.ChildLastNameEmpty);

		RuleFor(x => x.ChildDateOfBirth)
			.Cascade(CascadeMode.Stop)
			.Must(x => x != ImportDateValues.EmptyDate)
				.WithMessage(ValidationMessages.ChildDOBEmpty)
			.Must(x => x != ImportDateValues.InvalidDate)
				.WithMessage(ValidationMessages.ChildDOBInvalid);

		RuleFor(x => x.ChildPostCode.ToString())
			.Cascade(CascadeMode.Stop)
			.NotEmpty()
				.WithMessage(ValidationMessages.ChildPostCodeEmpty)
			.Must(DataValidation.BeAValidPostcode)
				.WithMessage(ValidationMessages.ChildPostCodeInvalid);

		//Partner - Optional cells when any are present all fields must be valid
		When(x => HasAnyPartnerDetail(x), () =>
		{
			RuleFor(x => x.PartnerFirstName)
				.NotEmpty()
				.WithMessage(ValidationMessages.PartnerFirstNameEmpty);

			RuleFor(x => x.PartnerLastName)
				.NotEmpty()
				.WithMessage(ValidationMessages.PartnerLastNameEmpty);

			RuleFor(x => x.PartnerNationalInsuranceNumber)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage(ValidationMessages.PartnerNINumberEmpty)
				.Must(DataValidation.BeAValidNi)
					.WithMessage(ValidationMessages.PartnerNINumberInvalid);

			RuleFor(x => x.PartnerDateOfBirth)
				.NotNull()
				.Must(x => x != ImportDateValues.InvalidDate)
				.WithMessage(ValidationMessages.PartnerDOBInvalid);
		});

		//SubmissionDate
		RuleFor(x => x.SubmissionDate)
			.Cascade(CascadeMode.Stop)
			.Must(x => x != ImportDateValues.EmptyDate)
				.WithMessage(ValidationMessages.SubmissionDateEmpty)
			.Must(x => x != ImportDateValues.InvalidDate)
				.WithMessage(ValidationMessages.SubmissionDateInvalid)
			.Must(DataValidation.BeAPastDate)
				.WithMessage(ValidationMessages.SubmissionDateInFuture);
	}
	private static bool HasAnyPartnerDetail(WorkingFamiliesEvent x)
	{
		return
			!string.IsNullOrWhiteSpace(x.PartnerFirstName) ||
			!string.IsNullOrWhiteSpace(x.PartnerLastName) ||
			x.PartnerDateOfBirth.HasValue ||
			!string.IsNullOrWhiteSpace(x.PartnerNationalInsuranceNumber);
	}
}