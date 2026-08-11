using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Helpers;
using CheckYourEligibility.API.Services;

namespace CheckYourEligibility.API.Usecases.Internal;

/// <summary>
///     Interface for processing a single eligibility check
/// </summary>
public interface IGetCheckWorkingFamiliesUseCase
{
    /// <summary>
    /// Apply buisiness logic for interal based eligibility checks
    /// </summary>
    /// <param name="eligibilityResponse"></param>
    /// <returns></returns>
    Task<CheckEligibilityItemResponse<CheckEligibilityWorkingFamiliesItem>> Execute(string guid, DateTime checkDate);
}

public class GetCheckWorkingFamiliesItemUseCase : IGetCheckWorkingFamiliesUseCase
{

    private readonly IGetEligibilityCheckItemService _getEligibilityCheckItemService;

    public GetCheckWorkingFamiliesItemUseCase(
        IGetEligibilityCheckItemService getEligibilityCheckItemService)
    {
        _getEligibilityCheckItemService = getEligibilityCheckItemService;
    }

    public async Task<CheckEligibilityItemResponse<CheckEligibilityWorkingFamiliesItem>> Execute(string guid, DateTime checkDate)
    {
        // get item and map check data for response
        var result = await _getEligibilityCheckItemService.GetEligibilityCheckItemAsync(guid);

        var item = _getEligibilityCheckItemService.MapCheckDataToResponseWorkingFamilies(result);

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