using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class BulkCheck
{
    public string Guid { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(100)")]
    public string ClientIdentifier { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(255)")]
    public string Filename { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(100)")]
    public CheckEligibilityType EligibilityType { get; set; }
    
    public DateTime SubmittedDate { get; set; }
    
    [Column(TypeName = "varchar(100)")]
    public string SubmittedBy { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(100)")]
    public BulkCheckStatus Status { get; set; }
    
    public virtual ICollection<EligibilityCheck> EligibilityChecks { get; set; } = new List<EligibilityCheck>();
}
