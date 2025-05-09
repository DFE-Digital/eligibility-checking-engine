using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class Establishment
{
    [Key] public int EstablishmentId { get; set; }

    public string EstablishmentName { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string Town { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public bool StatusOpen { get; set; }
    public int LocalAuthorityId { get; set; }
    public virtual LocalAuthority LocalAuthority { get; set; } = null!;

    [NotMapped] public double? LevenshteinDistance { get; set; }

    [Column(TypeName = "varchar(100)")] public string Type { get; set; } = string.Empty;
}