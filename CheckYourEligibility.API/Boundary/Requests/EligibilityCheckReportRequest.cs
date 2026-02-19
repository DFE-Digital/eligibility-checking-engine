public class EligibilityCheckReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GeneratedBy { get; set; }
    public int? LocalAuthorityID { get; set; }
    public CheckType CheckType { get; set; } = CheckType.BulkChecks;
}