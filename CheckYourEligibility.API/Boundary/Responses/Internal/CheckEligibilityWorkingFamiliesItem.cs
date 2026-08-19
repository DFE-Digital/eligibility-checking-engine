using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses.Internal
{

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CheckEligibilityWorkingFamiliesItem : CheckEligibilityItemBase
    {
        public TermValidity? TermValidity { get; set; }

        public ReconfirmationProperties? ReconfirmationProperties { get; set; }

        public bool? IsDiscretionaryValidityStartDateApplied { get; set; }

        public EligibilityCodeType? EligibilityCodeType {get;set;}
        public string? ValidityStartDate { get; set; }
        public string? DiscretionaryValidityStartDate { get; set; }
        public string ValidityEndDate { get; set; }
        public string GracePeriodEndDate { get; set; }
        public string EligibilityCode { get; set; }
        public string DateOfBirth { get; set; }
    }

    public class TermValidity { 
    
        public TermName? Current {get;set;} 
        public TermName? Next { get; set; }

        public TermValidity(TermName? current, TermName? next )
        {
            Current = current ?? TermName.None;
            Next = next ?? TermName.None;
        }
    }

    public class ReconfirmationProperties {

        public string? StartDate { get; set; }

        public string? EndDate { get; set; }

        public ReconfirmationStatus Status {get;set;}
      
    }
}
