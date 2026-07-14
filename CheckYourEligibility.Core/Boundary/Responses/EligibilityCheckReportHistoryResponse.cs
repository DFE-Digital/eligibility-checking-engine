namespace CheckYourEligibility.Core.Boundary.Responses;

public class EligibilityCheckReportHistoryResponse
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalNumberOfRecords { get; set; }
    public IEnumerable<EligibilityCheckReportHistoryItem> Data { get; set; }
}