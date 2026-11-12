namespace CheckYourEligibility.Core.Boundary.Responses;

public class EligibilityCheckReportHistoryItem
{
    public string ReportID { get; set; }
    public DateTime ReportGeneratedDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GeneratedBy { get; set; }
    public int NumberOfResults { get; set; }
    public string Status { get; set; }

}