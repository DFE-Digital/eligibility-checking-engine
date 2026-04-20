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
    public int MultiAcademyTrustID { get; set; }
    public string Name { get; set; }
    /// <summary>
    /// Indicates whether academies under this MAT are allowed to review evidence.
    /// Overrides LA-level setting.
    /// </summary>
    public bool AcademyCanReviewEvidence { get; set; }
    public virtual Collection<MultiAcademyTrustEstablishment> MultiAcademyTrustEstablishments { get; set; }
}