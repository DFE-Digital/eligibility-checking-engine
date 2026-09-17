using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Helpers;

namespace CheckYourEligibility.API.UseCases;

public interface IPreviewFosterFamilyCodeUseCase
{
    Task<FosterFamilyCodePreviewResponse> Execute(FosterFamilyRequest request, int localAuthorityId);
}

public class PreviewFosterFamilyCodeUseCase : IPreviewFosterFamilyCodeUseCase
{

    public async Task<FosterFamilyCodePreviewResponse> Execute(FosterFamilyRequest request, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterFamilyRequestValidator();
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        request.FosterCarer.LocalAuthorityID = localAuthorityId;

        var workingEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(request, "PREVIEW");

        // Term validity
        var termValidity = WorkingFamiliesCheckHelper.SetTermValidity(
            request.SubmissionDate,
            workingEvent.GracePeriodEndDate.ToString(),
            workingEvent.ValidityStartDate.ToString(),
            request.FosterChild.ChildDateOfBirth.ToString());

        // Reconfirmation properties       
        var reconfirmation = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
            workingEvent.ValidityEndDate.ToString(),
            workingEvent.GracePeriodEndDate.ToString(),
            request.SubmissionDate,
            EligibilityCodeType.Foster,
            request.FosterChild.ChildDateOfBirth.ToString());

        // Placeholder for actual eligibility code preview logic
        var response = new FosterFamilyCodePreviewResponse
        {
            ValidityStartDate = workingEvent.ValidityStartDate,
            ValidFromTerm = termValidity.Current.Name != TermName.None ? termValidity.Current : termValidity.Next,
            ReconfirmBetweenStart = reconfirmation.StartDate,
            ReconfirmBetweenEnd = reconfirmation.EndDate,
            GracePeriodEndDate = workingEvent.GracePeriodEndDate,
        };

        return response;

    }
}