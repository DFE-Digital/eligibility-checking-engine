public class EligibilityCheckReportHistoryItem
{
    public DateTime ReportGeneratedDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GeneratedBy { get; set; }
    public int NumberOfResults { get; set; }

}