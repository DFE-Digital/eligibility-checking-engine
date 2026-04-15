namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IEligibilityReporting
{
    Task<string> CreateEligibilityCheckReport(EligibilityCheckReportRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<EligibilityCheckReportHistoryItem>> GetEligibilityCheckReportHistory(string localAuthorityId);

    Task<EligibilityCheckReportResponse> GetEligibilityCheckReport(
        Guid reportId,
        int localAuthorityId,
        int pageNumber,
        CancellationToken cancellationToken = default);
}