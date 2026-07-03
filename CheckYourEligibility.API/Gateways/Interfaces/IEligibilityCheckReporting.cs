using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

public interface IEligibilityCheckReporting
{
    Task EligibilityCheckReports(Guid reportId, CheckEligibilityType eligiblityCheckType,string? source, CancellationToken cancellationToken = default);
    Task<EligibilityCheckReport> CreateReport(EligibilityCheckReportRequest request, CancellationToken cancellationToken = default);
    Task<EligibilityCheckReportHistoryResponse> GetEligibilityCheckReportHistory(string localAuthorityId, int pageNumber);
    Task<int> GetLocalAuthorityIdForReport(Guid reportId, CancellationToken cancellationToken = default);
    Task DeleteEligibilityCheckReport(Guid reportId, CancellationToken cancellationToken = default);
    Task<EligibilityCheckReport?> GetEligibilityReportById(Guid reportId);
    Task<Dictionary<Guid, EligibilityCheck>> GetEligibilityChecksByReportId(Guid reportId);

}