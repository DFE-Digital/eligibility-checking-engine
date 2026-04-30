using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CheckYourEligibility.API.Domain;

public class EligibilityCheckReportItems
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid EligibilityCheckReportItemId { get; init; } = Guid.NewGuid();
    
    public Guid EligibilityCheckReportId { get; set; }
    public virtual EligibilityCheckReport EligibilityCheckReport { get; set; }

    public string EligibilityCheckID { get; set; }
    public virtual EligibilityCheck EligibilityCheck { get; set; }

    public bool IsBulkCheckItem { get; set; }
    
}