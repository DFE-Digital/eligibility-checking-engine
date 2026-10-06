using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class WorkingFamiliesDualRunningCheck
{ 
    [Key]
    public int Id { get; set; }

    public string EligibilityCheckID { get; set; }
    public string EligibilityCode { get; set; }
    public string ECEStatus { get; set; }
    public string ECSStatus { get; set; }
    public string? ECSQualifier { get; set; }

    public bool isConflict { get; set; }
    public bool AreDatesMatching { get; set; }

    public string ECSResponseBody { get; set; }
    public string ECEResponseBody { get; set; }

    public DateTime Created { get; set; }

    public virtual EligibilityCheck EligibilityCheck { get; set; }

   
}