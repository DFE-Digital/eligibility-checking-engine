// Ignore Spelling: Fsm

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class EligibilityCheckHash
{
    public string EligibilityCheckHashID { get; set; } = string.Empty;

    [Column(TypeName = "varchar(5000)")] public string Hash { get; set; } = string.Empty;

    [Column(TypeName = "varchar(100)")] public CheckEligibilityType Type { get; set; }

    public DateTime TimeStamp { get; set; }

    [Column(TypeName = "varchar(100)")] public CheckEligibilityStatus Outcome { get; set; }

    [Column(TypeName = "varchar(100)")] public ProcessEligibilityCheckSource Source { get; set; }
}