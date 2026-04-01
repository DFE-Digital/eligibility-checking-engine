using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class WorkingFamiliesEventSummary
{
    public string WorkingFamiliesEventSummaryID { get; set; }
    
    [Column(TypeName = "nchar(11)")] public string EligibilityCode { get; set; }

	public int OwningLocalAuthorityId { get; set; }

    public bool HasCodeBeenCheckedByOwningLA { get; set; }

	public DateTime ChildDateOfBirth { get; set; }

	[Column(TypeName = "nvarchar(9)")] public string? ChildPostCode { get; set; }

	[Column(TypeName = "varchar(100)")] public string ChildFirstName { get; set; }

    [Column(TypeName = "varchar(26)")] public string ChildFirstNameTruncated { get; set; }

	[Column(TypeName = "varchar(10)")] public string? ParentNationalInsuranceNumber { get; set; }

	[Column(TypeName = "varchar(10)")] public string? PartnerNationalInsuranceNumber { get; set; }

    public DateTime ValidityStartDate { get; set; }

    public DateTime ValidityEndDate { get; set; }

	public DateTime GracePeriodEndDate { get; set; }

	public DateTime LastUpdatedDate { get; set; }

	public DateTime? LastCheckDate { get; set; }

	public DateTime? FirstCheckDate { get; set; }

	public DateTime? FirstEventDate { get; set; }

	public DateTime? DiscretionaryValidityStartDate { get; set; }

	public DateTime? LatestSubmissionDate { get; set; }

	public int? FirstCheckLocalAuthorityId { get; set; }

	public int? LastCheckLocalAuthorityId { get; set; }

	[Column(TypeName = "nvarchar(50)")] public string? Qualifier { get; set; }

}