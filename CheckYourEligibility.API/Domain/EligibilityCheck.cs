// Ignore Spelling: Fsm

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class EligibilityCheck
{
    public string EligibilityCheckID { get; set; } = string.Empty;

    [Column(TypeName = "varchar(100)")] public CheckEligibilityType Type { get; set; }

    [Column(TypeName = "varchar(100)")] public CheckEligibilityStatus Status { get; set; }

    public DateTime Created { get; set; }

    public DateTime Updated { get; set; }

    public string? EligibilityCheckHashID { get; set; }

    public virtual EligibilityCheckHash? EligibilityCheckHash { get; set; }

    public string? Group { get; set; }

    public int? Sequence { get; set; }
    public string CheckData { get; set; } = string.Empty;
}