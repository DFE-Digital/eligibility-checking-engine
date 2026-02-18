using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CheckYourEligibility.API.Domain;

public class EligibilityCheckReport
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid EligibilityCheckReportId { get; init; } = Guid.NewGuid();
    public DateTime ReportGeneratedDate { get; init; } = DateTime.UtcNow;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GeneratedBy { get; set; }
    public int NumberOfResults { get; set; }
    public int? LocalAuthorityID { get; set; }
    public virtual LocalAuthority? LocalAuthority { get; set; }
    public CheckType CheckType { get; set; } = CheckType.BulkChecks;
}