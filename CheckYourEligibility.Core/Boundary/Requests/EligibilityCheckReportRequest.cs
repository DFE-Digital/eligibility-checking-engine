using CheckYourEligibility.Core.Domain.Enums;

public class EligibilityCheckReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GeneratedBy { get; set; }
    public int? LocalAuthorityID { get; set; }
    public bool SaveRequestAudit { get; set; }
    public CheckType CheckType { get; set; } = CheckType.BulkChecks;
    public CheckEligibilityType EligibilityCheckType { get; set; } = CheckEligibilityType.FreeSchoolMeals;
}