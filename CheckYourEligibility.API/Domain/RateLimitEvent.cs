using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class RateLimitEvent
{
    [Key] public string RateLimitEventId { get; set; }
    public DateTime TimeStamp { get; set; }

    [Column(TypeName = "varchar(100)")] public string PartitionName { get; set; }

    public int QuerySize { get; set; }

    public bool Accepted { get; set; }
}