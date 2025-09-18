using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class MultiAcademyTrustSchool
{
    [Key] public int ID { get; set; } //TODO: AutooGenerated
    public int TrustId { get; set; }
    public int SchoolId { get; set; } //TODO: Should be EstablishmentId???? To keep naming consistent
    public virtual MultiAcademyTrust MultiAcademyTrust { get; set; }
}