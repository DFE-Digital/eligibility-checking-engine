using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class MultiAcademyTrustEstablishment
{
    [Key] public int MultiAcademyTrustEstablishmentID { get; set; }
    public int MultiAcademyTrustID { get; set; }
    public int EstablishmentID { get; set; }
    public virtual MultiAcademyTrust MultiAcademyTrust { get; set; }
}