using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Helpers;
using CheckYourEligibility.API.Services;
using CheckYourEligibility.API.UseCases;

namespace CheckYourEligibility.API.Usecases.Internal;

/// <summary>
///     Interface for processing a single eligibility check
/// </summary>
public interface IGetCheckWorkingFamiliesUseCase
{
    /// <summary>
    /// Apply buisiness logic for internal based eligibility checks
    /// </summary>
    /// <param name="eligibilityResponse"></param>
    /// <returns></returns>
    Task<CheckEligibilityItemResponse<CheckEligibilityWorkingFamiliesItem>> Execute(string guid, DateTime checkDate);
}

public class GetCheckWorkingFamiliesItemUseCase : IGetCheckWorkingFamiliesUseCase
{

    private readonly IEligiblityCheckDataResponseMapper _getEligibilityCheckItemService;
    private readonly ILogger<GetEligibilityCheckItemUseCase> _logger;
    private readonly ICheckEligibility _checkGateway;

    private static string SanitizeForLog(string value)
    {
        return value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;
    }

    public GetCheckWorkingFamiliesItemUseCase(
        IEligiblityCheckDataResponseMapper getEligibilityCheckItemService, ILogger<GetEligibilityCheckItemUseCase> logger, ICheckEligibility checkGateway)
    {
        _getEligibilityCheckItemService = getEligibilityCheckItemService;
        _logger = logger;
        _checkGateway = checkGateway;
    }

    public async Task<CheckEligibilityItemResponse<CheckEligibilityWorkingFamiliesItem>> Execute(string guid, DateTime checkDate)
    {
        // get item and map check data for response

        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

        var sanitizedGuidForLog = SanitizeForLog(guid);

        var result = await _checkGateway.GetItem(guid);
        if (result == null)
        {
            _logger.LogWarning(
              "Eligibility check with ID {Guid} not found", sanitizedGuidForLog);
            throw new NotFoundException(guid);
        }

        _logger.LogInformation(
            "Retrieved eligibility check details for ID: {Guid}", sanitizedGuidForLog);

        var item = _getEligibilityCheckItemService.MapCheckDataToResponseWorkingFamilies(result, isInternal:true);

        item.EligibilityCodeType = WorkingFamiliesCheckHelper.GetEligibilityCodeType(item.EligibilityCode);

        item.IsDiscretionaryValidityStartDateApplied =
        WorkingFamiliesCheckHelper.IsDiscretionaryValidityStartDateApplied(item.ValidityStartDate, item.DiscretionaryValidityStartDate);

        item.TermValidity = WorkingFamiliesCheckHelper.SetTermValidity(checkDate, item.GracePeriodEndDate, item.ValidityStartDate, item.DateOfBirth);

        item.ReconfirmationProperties = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
           item.ValidityEndDate,
           item.GracePeriodEndDate,
           checkDate,
           item.EligibilityCodeType,
           item.DateOfBirth);

        return new CheckEligibilityItemResponse<CheckEligibilityWorkingFamiliesItem>
        {
            Data = item,
            Links = new CheckEligibilityResponseLinks
            {

                Get_EligibilityCheck = $"{CheckLinks.InternalWorkingFamiliesGetLink}{guid}",
                Put_EligibilityCheckProcess = $"{CheckLinks.ProcessLink}{guid}",
                Get_EligibilityCheckStatus = $"{CheckLinks.GetLink}{guid}/Status"
            }
        };

    }
}