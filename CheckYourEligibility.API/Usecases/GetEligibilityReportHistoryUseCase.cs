using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.API.Gateways.Interfaces;

public interface IGetEligibilityReportHistoryUseCase
{
    Task<EligibilityCheckReportHistoryResponse> Execute(string localAuthorityId, IList<int> localAuthorityIds);
}

public class GetEligibilityReportHistoryUseCase :  IGetEligibilityReportHistoryUseCase
{
    private readonly  ICheckEligibility _checkEligibilityGateway;
    private readonly ILogger<GetEligibilityReportHistoryUseCase> _logger;

    public GetEligibilityReportHistoryUseCase(ICheckEligibility checkEligibilityGateway, ILogger<GetEligibilityReportHistoryUseCase> logger)
    {
        _checkEligibilityGateway = checkEligibilityGateway;
        _logger = logger;
    }

    public async Task<EligibilityCheckReportHistoryResponse> Execute(string localAuthorityId, IList<int> localAuthorityIds)
    {
        if(string.IsNullOrEmpty(localAuthorityId)) throw new ValidationException("Local Authority ID is required");
        
        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(int.Parse(localAuthorityId)))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to view report history for this Local Authority");
        };

        var response = await _checkEligibilityGateway.GetEligibilityCheckReportHistory(localAuthorityId);

        // Sanitize user-provided Local Authority ID before logging to prevent log forging
        var sanitizedLocalAuthorityId = localAuthorityId?.Replace("\r", string.Empty).Replace("\n", string.Empty);

        if (response == null)
        {
            _logger.LogError("Failed to retrieve eligibility check report history for Local Authority ID: {LocalAuthorityId}", sanitizedLocalAuthorityId);
            throw new Exception("Failed to retrieve eligibility check report history");
        }

        _logger.LogInformation("Successfully generated eligibility check report history for Local Authority ID: {LocalAuthorityId}", sanitizedLocalAuthorityId);

        return new EligibilityCheckReportHistoryResponse
        {
           Data = response
        };
    }
}