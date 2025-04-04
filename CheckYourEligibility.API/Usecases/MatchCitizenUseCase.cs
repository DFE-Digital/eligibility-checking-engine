using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses.DWP;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;

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

    public async Task<DwpMatchResponse?> Execute(CitizenMatchRequest model)
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
}