using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain
{
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
        public string ECSValidityDates { get; set; }
        public string ECEValidityDates { get; set; }
        public DateTime Created { get; set; }
        public virtual EligibilityCheck EligibilityCheck { get; set; }
    }
}