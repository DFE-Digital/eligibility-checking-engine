namespace CheckYourEligibility.API.Boundary.Responses
{
    public class FosterCodePreviewResponse
    {
        public DateTime ValidityStartDate { get; set; }

        public DateTime ValidityEndDate { get; set; }

        public DateTime GracePeriodEndDate { get; set; }

        public bool IsGracePeriodEndDateApplied { get; set; }

        public ReconfirmationProperties ReconfirmationProperties { get; set; }

        public TermValidity TermValidity { get; set; }
        
        public bool ChildTooYoung { get; set; }
    }
}