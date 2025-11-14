// Ignore Spelling: Fsm

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class Audit
{
    public string AuditID { get; set; }

    [Column(TypeName = "varchar(100)")] public AuditType Type { get; set; }

    [Column(TypeName = "varchar(200)")] public string TypeID { get; set; }

    [Column(TypeName = "varchar(200)")] public string Url { get; set; }

    [Column(TypeName = "varchar(200)")] public string Method { get; set; }

    [Column(TypeName = "varchar(500)")] public string Source { get; set; }

    [Column(TypeName = "varchar(5000)")] public string Authentication { get; set; }

    [Column(TypeName = "varchar(500)")] public string? Scope { get; set; }

    public DateTime TimeStamp { get; set; }
}