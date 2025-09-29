using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class MultiAcademyTrust
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int UID { get; set; }
    public string Name { get; set; }
    public virtual Collection<MultiAcademyTrustSchool> MultiAcademyTrustSchools { get; set; }
}