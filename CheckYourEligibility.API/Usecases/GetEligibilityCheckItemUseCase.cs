using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Services;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving eligibility check item details
/// </summary>
public interface IGetEligibilityCheckItemUseCase
{
    /// <summary>
    ///     Execute the use case for client side API
    /// </summary>
    /// <param name="guid">The ID of the eligibility check</param>
    /// <param name="type">The type of the eligibility check being retrieved (Optional)</param> 
    /// <returns>Eligibility check item details</returns>
    Task<CheckEligibilityItemResponse<CheckEligibilityItemBase>> Execute(string guid, CheckEligibilityType type);
}

public class GetEligibilityCheckItemUseCase : IGetEligibilityCheckItemUseCase
{
    private readonly IGetEligibilityCheckItemService _getEligibilityCheckItemService;

    public GetEligibilityCheckItemUseCase(
        IGetEligibilityCheckItemService getEligibilityCheckItemService)
    {
        _getEligibilityCheckItemService = getEligibilityCheckItemService;

    }

    public async Task<CheckEligibilityItemResponse<CheckEligibilityItemBase>> Execute(string guid, CheckEligibilityType type)
    {
        // get item
        var result = await _getEligibilityCheckItemService.GetEligibilityCheckItemAsync(guid);
        var response =_getEligibilityCheckItemService.MapCheckDataToResponse(result);

        string typeUrl = "";
        if (type != CheckEligibilityType.None)
        {
            typeUrl = $"{type}/";
        }

        return new CheckEligibilityItemResponse<CheckEligibilityItemBase>
        {
            Data = response,
            Links = new CheckEligibilityResponseLinks
            {
                Get_EligibilityCheck = $"{CheckLinks.GetLink}{typeUrl}{guid}",
                Put_EligibilityCheckProcess = $"{CheckLinks.ProcessLink}{guid}",
                Get_EligibilityCheckStatus = $"{CheckLinks.GetLink}{typeUrl}{guid}/Status"
            }
        };
    }
}