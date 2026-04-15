using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

public interface IGetEligibilityReportUseCase
{
    Task<EligibilityCheckReportResponse> Execute(Guid reportId, string localAuthorityId, IList<int> localAuthorityIds, int pageNumber);
}

public class GetEligibilityCheckReportUseCase : IGetEligibilityReportUseCase
{
    private readonly IEligibilityReporting _eligibilityReportingGateway;
    private readonly ILogger<GetEligibilityCheckReportUseCase> _logger;

    public GetEligibilityCheckReportUseCase(
        IEligibilityReporting eligibilityReportingGateway,
        ILogger<GetEligibilityCheckReportUseCase> logger)
    {
        _eligibilityReportingGateway = eligibilityReportingGateway;
        _logger = logger;
    }

    public async Task<EligibilityCheckReportResponse> Execute(Guid reportId, string localAuthorityId, IList<int> localAuthorityIds, int pageNumber)
    {
        const int maxPageNumber = 1000;
        const int pageSize = 100;

        if (reportId == Guid.Empty)
            throw new ValidationException("Report ID is required");

        if (string.IsNullOrEmpty(localAuthorityId))
            throw new ValidationException("Local Authority ID is required");

        if (!int.TryParse(localAuthorityId, out var requestedLocalAuthorityId))
            throw new ValidationException("Local Authority ID must be a valid integer");

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(requestedLocalAuthorityId))
            throw new UnauthorizedAccessException("You do not have permission to view this report for the requested Local Authority");

        if (pageNumber < 1 || pageNumber > maxPageNumber)
            throw new ValidationException($"Page number must be between 1 and {maxPageNumber}");

        var response = await _eligibilityReportingGateway.GetEligibilityCheckReport(reportId, requestedLocalAuthorityId, pageNumber);

        if (response == null)
        {
            _logger.LogError("No eligibility check report found for ReportId {ReportId} and LocalAuthorityId {LocalAuthorityId}", reportId, requestedLocalAuthorityId);
            throw new KeyNotFoundException($"Report {reportId} not found");
        }

        _logger.LogInformation("Retrieved eligibility check report {ReportId} page {PageNumber} size {PageSize}", reportId, pageNumber, pageSize);

        return response;
    }
}