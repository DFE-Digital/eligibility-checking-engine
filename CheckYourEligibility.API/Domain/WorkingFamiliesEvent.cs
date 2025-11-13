using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class WorkingFamiliesEvent
{
    public string WorkingFamiliesEventId { get; set; }
    [Column(TypeName = "nchar(11)")] public string EligibilityCode { get; set; }
    [Column(TypeName = "varchar(100)")] public string ChildFirstName { get; set; }

    [Column(TypeName = "varchar(100)")] public string ChildLastName { get; set; }
    public DateTime ChildDateOfBirth { get; set; }
    [Column(TypeName = "varchar(100)")] public string ParentFirstName { get; set; }

    [Column(TypeName = "varchar(100)")] public string ParentLastName { get; set; }

    [Column(TypeName = "varchar(10)")] public string? ParentNationalInsuranceNumber { get; set; }

    [Column(TypeName = "varchar(100)")] public string PartnerFirstName { get; set; }

    [Column(TypeName = "varchar(100)")] public string PartnerLastName { get; set; }

    [Column(TypeName = "varchar(10)")] public string? PartnerNationalInsuranceNumber { get; set; }
    public DateTime SubmissionDate { get; set; }
    public DateTime ValidityStartDate { get; set; }
    public DateTime ValidityEndDate { get; set; }
    public DateTime DiscretionaryValidityStartDate { get; set; }
    public DateTime GracePeriodEndDate { get; set; }

    public string getHash()
    {
        //Exclude event Id
        return $"""
                {EligibilityCode}
                {ChildFirstName}
                {ChildLastName}
                {ChildDateOfBirth}
                {ParentFirstName}
                {ParentLastName}
                {ParentNationalInsuranceNumber}
                {PartnerFirstName}
                {PartnerLastName}
                {PartnerNationalInsuranceNumber}
                {SubmissionDate}
                {ValidityStartDate}
                {ValidityEndDate}
                {DiscretionaryValidityStartDate}
                {GracePeriodEndDate}
                """.ReplaceLineEndings("");

    }
}