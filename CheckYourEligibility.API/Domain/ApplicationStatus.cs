// Ignore Spelling: Fsm

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class ApplicationStatus
{
    public string ApplicationStatusID { get; set; } = string.Empty;
    public string ApplicationID { get; set; } = string.Empty;
    public virtual Application Application { get; set; } = null!;

    [Column(TypeName = "varchar(100)")] public Enums.ApplicationStatus Type { get; set; }

    public DateTime TimeStamp { get; set; }
}