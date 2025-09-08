// Ignore Spelling: Fsm

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

/// <summary>
/// Represents an individual eligibility check
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class EligibilityCheck
{
    /// <summary>
    /// The unique identifier for the eligibility check
    /// </summary>
    public string EligibilityCheckID { get; set; } = string.Empty;

    /// <summary>
    /// The type of eligibility check
    /// </summary>
    [Column(TypeName = "varchar(100)")] public CheckEligibilityType Type { get; set; }

    /// <summary>
    /// The current status of the eligibility check
    /// </summary>
    [Column(TypeName = "varchar(100)")] public CheckEligibilityStatus Status { get; set; }

    /// <summary>
    /// The date and time when the check was created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// The date and time when the check was last updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// The hash ID associated with this eligibility check
    /// </summary>
    public string? EligibilityCheckHashID { get; set; }

    /// <summary>
    /// The eligibility check hash entity
    /// </summary>
    public virtual EligibilityCheckHash? EligibilityCheckHash { get; set; }

    /// <summary>
    /// The group identifier for bulk operations
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// The bulk check entity if this is part of a bulk operation
    /// </summary>
    public virtual BulkCheck? BulkCheck { get; set; }
    
    /// <summary>
    /// The serialized check data
    /// </summary>
    public string CheckData { get; set; } = string.Empty;
}