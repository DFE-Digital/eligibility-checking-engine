using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

namespace CheckYourEligibility.API.Boundary.Responses.Internal
{


    public class CheckWorkingFamiliesResponse : CheckEligibilityItem
    {
        public TermValidity TermValidity { get; set; }

        public ReconfirmationProperties ReconfirmationProperies { get; set; }

        public bool IsDiscretionaryValidityStartDateApplied { get; set; }

        public EligibilityCodeType EligibilityCodeType {get;set;}

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

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public ReconfirmationStatus Status {get;set;}
      
    }
}
