using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class Establishment
{
    [Key] public int EstablishmentID { get; set; }

    public string EstablishmentName { get; set; }
    public string Postcode { get; set; }
    public string Street { get; set; }
    public string Locality { get; set; }
    public string Town { get; set; }
    public string County { get; set; }
    public bool StatusOpen { get; set; }
    public int LocalAuthorityID { get; set; }
    public virtual LocalAuthority LocalAuthority { get; set; }

    [NotMapped] public double? LevenshteinDistance { get; set; }

    [Column(TypeName = "varchar(100)")] public string Type { get; set; }
}