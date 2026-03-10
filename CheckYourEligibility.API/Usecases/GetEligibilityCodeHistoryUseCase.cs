using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.Drawing.Charts;

/// <summary>
///     Interface for retrieving eligibility check status
/// </summary>
public interface IGetEligibilityCodeHistoryUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="eligibilityCode">The ID of the eligibility check</param>
    /// <returns>Eligibility Code history</returns>
    Task<EligibilityCodeHistoryResponse> Execute(string eligibilityCode);
}

public class GetEligibilityCodeHistoryUseCase : IGetEligibilityCodeHistoryUseCase
{
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<GetEligibilityCodeHistoryUseCase> _logger;

    public async Task<EligibilityCodeHistoryResponse> Execute(string eligibilityCode)
    {
        if (string.IsNullOrEmpty(eligibilityCode)) throw new ValidationException(null, "Invalid Request, Eligibility Code is required.");

        var response = await _checkGateway.GetEligibilityCodeHistory(eligibilityCode);

        if (response == null)
        {
            _logger.LogWarning(
                $"Eligibility code {eligibilityCode.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} history not found");
            throw new NotFoundException(eligibilityCode);

        }

        return new EligibilityCodeHistoryResponse
        {
            Data = response
        };
    }
}