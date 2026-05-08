public interface IEligibilityCheckReporting
{
    Task EligibilityCheckReports(Guid reportId, CancellationToken cancellationToken = default);
    Task<EligibilityCheckReport> CreateReport(EligibilityCheckReportRequest request, CancellationToken cancellationToken = default);
    Task<EligibilityCheckReportHistoryResponse> GetEligibilityCheckReportHistory(string localAuthorityId, int pageNumber); 
}