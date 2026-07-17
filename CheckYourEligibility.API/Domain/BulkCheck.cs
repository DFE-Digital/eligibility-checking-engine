using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class BulkCheck
{
    public string BulkCheckID { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(255)")]
    public string Filename { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(100)")]
    public CheckEligibilityType EligibilityType { get; set; }
    
    public DateTime SubmittedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    [Column(TypeName = "varchar(100)")]
    public string SubmittedBy { get; set; } = string.Empty;
    
    [Column(TypeName = "varchar(100)")]
    public BulkCheckStatus Status { get; set; }

    /// <summary>
    /// ID of Organisation if found in scope
    /// else OrganisationID = 0
    /// </summary>
    [Column(TypeName = "int")]
    public int? OrganisationID { get; set; }

    /// <summary>
    /// What type of organisation is making the check
    /// It can be local-authority, establishment multi-academy-trust
    /// else it will be set to NULL
    /// </summary>
    [Column(TypeName = "nvarchar(20)")]
    public string? OrganisationType { get; set; }

    
    /// <summary>
    /// The Local Authority ID that submitted this bulk check
    /// </summary>
    public int? LocalAuthorityID { get; set; }
    
    /// <summary>
    /// Navigation property to the Local Authority
    /// </summary>
    public virtual LocalAuthority? LocalAuthority { get; set; }

    public virtual ICollection<EligibilityCheck> EligibilityChecks { get; set; } = new List<EligibilityCheck>();

    public string FinalNameInCheck { get; set; } = string.Empty;
    public int NumberOfRecords { get; set; } 
}