using CheckYourEligibility.Core.Domain.Enums.WorkingFamilies;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses.Internal
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
        public bool? ChildTooYoung { get; set; }
    }

}
