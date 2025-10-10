using Azure;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.DWP;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Newtonsoft.Json;
using System.Net;
using System;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for matching a citizen.
/// </summary>
public interface IMatchCitizenUseCase
{
    /// <summary>
    ///     Execute the use case.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    Task<DwpMatchResponse> Execute(CitizenMatchRequest model);
    CAPICitizenResponse ProcessCitizenCheck(HttpResponseMessage response);
}

/// <summary>
///     Use case for matching a citizen.
/// </summary>
public class MatchCitizenUseCase : IMatchCitizenUseCase
{
    private readonly ICheckEligibility _gateway;

    public MatchCitizenUseCase(ICheckEligibility gateway)
    {
        _gateway = gateway;
    }

    public async Task<DwpMatchResponse> Execute(CitizenMatchRequest model)
    {
        // Implement the logic for matching a citizen here
        if (model?.Data?.Attributes?.LastName.ToUpper() == MogDWPValues.validCitizenSurnameEligible.ToUpper()
            || model?.Data?.Attributes?.LastName.ToUpper() == MogDWPValues.validCitizenSurnameNotEligible.ToUpper())
            return new DwpMatchResponse
            {
                Data = new DwpMatchResponse.DwpResponse_Data
                {
                    Id = model?.Data?.Attributes?.LastName.ToUpper() ==
                         MogDWPValues.validCitizenSurnameEligible.ToUpper()
                        ? MogDWPValues.validCitizenEligibleGuid
                        : MogDWPValues.validCitizenNotEligibleGuid,
                    Type = "MatchResult",
                    Attributes = new DwpMatchResponse.DwpResponse_Attributes { MatchingScenario = "FSM" }
                },
                Jsonapi = new DwpMatchResponse.DwpResponse_Jsonapi { Version = "2.0" }
            };

        if (model?.Data?.Attributes?.LastName.ToUpper() == MogDWPValues.validCitizenSurnameDuplicatesFound.ToUpper())
            throw new InvalidOperationException("Duplicates found");

        return null;
    }
    public CAPICitizenResponse ProcessCitizenCheck(HttpResponseMessage response)
    {
        // ECS_Conflict helper logic to better track conflicts
        CAPICitizenResponse citizenResponse = new();
        citizenResponse.Guid = string.Empty;
        citizenResponse.Reason = string.Empty;
        citizenResponse.CAPIEndpoint = "/v2/citizens/match";
        citizenResponse.CAPIResponseCode = response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            var responseData =
                JsonConvert.DeserializeObject<DwpMatchResponse>(response.Content.ReadAsStringAsync().Result);
              citizenResponse.Guid = responseData.Data.Id;
            citizenResponse.Reason = "CAPI returned an ID. Citizen found.";

        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        {

            citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
            citizenResponse.Reason = "Citizen not found.";

        }
        else if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {

            citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
            citizenResponse.Reason = "Unprocessable Entity - The request was well-formed but was unable to be performed due to validation/semantic errors.";

        }
        else
        {
            string errorMessage = $"Get Citizen failed. uri, Response:- {response.StatusCode}";
            citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
            citizenResponse.Reason = errorMessage;
           
        }

        return citizenResponse;
    }
}
