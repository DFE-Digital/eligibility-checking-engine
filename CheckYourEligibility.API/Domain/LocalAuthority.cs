using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class LocalAuthority
{
    [Key] public int LocalAuthorityID { get; set; }

    public string LaName { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string? Region { get; set; }

    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Indicates whether schools belonging to this Local Authority are allowed
    /// to review evidence and access the Pending Applications and Guidance tiles
    /// in the FSM admin portal.
    /// </summary>
    public bool SchoolCanReviewEvidence { get; set; } = false;

    public int FreeSchoolMealsPolicyID { get; set; }

    public int EarlyYearsPupilPremiumPolicyID { get; set; }

    public int TwoYearPolicyID { get; set; }

}