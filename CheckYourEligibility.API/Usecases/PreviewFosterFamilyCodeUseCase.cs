using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Helpers;
using Microsoft.AspNetCore.Http.Features;

namespace CheckYourEligibility.API.UseCases;

public interface IPreviewFosterFamilyCodeUseCase
{
    Task<FosterCodePreviewResponse> Execute(FosterFamilyRequest request, int localAuthorityId);
}

public class PreviewFosterFamilyCodeUseCase : IPreviewFosterFamilyCodeUseCase
{
    private readonly bool _enforceSubmissionDateWindow;

    public PreviewFosterFamilyCodeUseCase(IConfiguration? configuration = null)
    {
        _enforceSubmissionDateWindow = configuration?.GetValue("FosterFamilies:EnforceSubmissionDateWindow", true) ?? true;
    }

    public async Task<FosterCodePreviewResponse> Execute(FosterFamilyRequest request, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterFamilyRequestValidator(_enforceSubmissionDateWindow);
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        request.FosterCarer.LocalAuthorityID = localAuthorityId;

        var workingEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyEventFromFosterFamily(
            FosterFamiliesHelper.BuildFosterCarer(request.FosterCarer, request.Partner, request.HasPartner),
            FosterFamiliesHelper.BuildFosterChild(request.FosterChild, request.SubmissionDate, Guid.Empty),
            "PREVIEW",
            request.SubmissionDate,
            request.SubmissionDate
        );

        //IsGracePeriodEndDateApplied
        bool isGracePeriodEndDateApplied = WorkingFamiliesCheckHelper.IsGracePeriodEndDateApplied(workingEvent.DiscretionaryValidityStartDate, workingEvent.ValidityEndDate, 1, workingEvent.ChildDateOfBirth, request.SubmissionDate);
        
        // Term validity
        var termValidity = WorkingFamiliesCheckHelper.SetTermValidity(
            DateTime.Today,
            workingEvent.GracePeriodEndDate.ToString(),
            workingEvent.ValidityStartDate.ToString(),
            request.FosterChild.ChildDateOfBirth.ToString(), isGracePeriodEndDateApplied);

        // Reconfirmation properties       
        var reconfirmation = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
            workingEvent.ValidityEndDate.ToString(),
            workingEvent.GracePeriodEndDate.ToString(),
            request.SubmissionDate,
            EligibilityCodeType.Foster,
            request.FosterChild.ChildDateOfBirth.ToString());

        // Placeholder for actual eligibility code preview logic
        var response = new FosterCodePreviewResponse
        {
            ValidityStartDate = workingEvent.ValidityStartDate,
            ValidityEndDate = workingEvent.ValidityEndDate,
            GracePeriodEndDate = workingEvent.GracePeriodEndDate,
            TermValidity = termValidity,
            ReconfirmationProperties = reconfirmation,
            IsGracePeriodEndDateApplied = isGracePeriodEndDateApplied,
            ChildTooYoung = WorkingFamiliesCheckHelper.ChildIsTooYoung(request.FosterChild.ChildDateOfBirth, request.SubmissionDate),
            ChildTooOld = WorkingFamiliesCheckHelper.ChildIsTooOld(request.FosterChild.ChildDateOfBirth, request.SubmissionDate)
        };

        return response;

    }
}