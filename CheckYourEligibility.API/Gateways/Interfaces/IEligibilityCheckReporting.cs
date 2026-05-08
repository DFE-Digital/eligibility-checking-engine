public interface IEligibilityCheckReporting
{
    Task<IEnumerable<EligibilityCheckReportResponseItem>> EligibilityCheckReports(EligibilityCheckReportRequest request, CancellationToken cancellationToken = default);
    Task<EligibilityCheckReportHistoryResponse> GetEligibilityCheckReportHistory(string localAuthorityId, int pageNumber); 
}