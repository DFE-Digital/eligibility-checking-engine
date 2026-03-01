using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class LocalAuthority
{
    [Key] public int LocalAuthorityID { get; set; }

    public string LaName { get; set; }

    /// <summary>
    /// Indicates whether schools belonging to this Local Authority are allowed
    /// to review evidence and access the Pending Applications and Guidance tiles
    /// in the FSM admin portal.
    /// </summary>
    public bool SchoolCanReviewEvidence { get; set; } = false;


}