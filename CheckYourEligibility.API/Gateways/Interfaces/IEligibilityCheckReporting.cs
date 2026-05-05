public interface IEligibilityCheckReporting
{
    Task<IEnumerable<EligibilityCheckReportResponseItem>> EligibilityCheckReports(EligibilityCheckReportRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<EligibilityCheckReportHistoryItem>> GetEligibilityCheckReportHistory(string localAuthorityId); 
}