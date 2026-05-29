public class EligibilityCheckReportRequest
{
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public string GeneratedBy { get; set; }
    public int? LocalAuthorityID { get; set; }
    public bool SaveRequestAudit { get; set; }
    public CheckType CheckType { get; set; } = CheckType.BulkChecks;
}