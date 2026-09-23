using System.Text.Json.Serialization;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

namespace CheckYourEligibility.API.Boundary.Responses
{

    public class FosterChildResponse
    {
        // Eligibility Code Details

        public string EligibilityCode { get; set; } = string.Empty;

        public DateTime ValidityStartDate { get; set; }

        public DateTime ValidityEndDate { get; set; }

        public DateTime GracePeriodEndDate { get; set; }

        public ReconfirmationProperties ReconfirmationProperties { get; set; }

        public TermValidity TermValidity { get; set; }
        
        public bool ChildTooYoung { get; set; }


        // Child
        public Guid FosterChildId { get; set; }

        [Obsolete("To be removed in future - use ChildFirstName and ChildLastName")]
        public string ChildFullName { get; set; }

        public string ChildFirstName { get; set; }

        public string ChildLastName { get; set; }

        public DateTime ChildDateOfBirth { get; set; }

        [Obsolete("To be removed in future - use ChildPostCode")]
        public string PostCode { get; set; }

        public string ChildPostCode { get; set; }


        // Foster Family

        public Guid? FosterCarerId { get; set; }

        public string? CarerName { get; set; }

        public string? PartnerName { get; set; }

    }
}