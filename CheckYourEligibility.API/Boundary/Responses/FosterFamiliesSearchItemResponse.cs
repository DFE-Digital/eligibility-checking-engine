namespace CheckYourEligibility.API.Boundary.Responses
{
    public class FosterFamiliesSearchItemResponse
    {
        public string ChildName { get; set; } = string.Empty;

        public DateTime ChildDateOfBirth { get; set; }

        public string CarerName { get; set; } = string.Empty;

        public Guid FosterCarerId { get; set; }

        public Guid FosterChildId { get; set; }

        public string EligibilityCode { get; set; } = string.Empty;

        public DateTime ValidityStartDate { get; set; }

        public DateTime ValidityEndDate { get; set; }

        public DateTime GracePeriodEndDate { get; set; }

        public bool IsGracePeriodEndDateApplied { get; set; }

        public ReconfirmationProperties ReconfirmationProperties { get; set; }

        public TermValidity TermValidity { get; set; }

        public bool ChildTooYoung { get; set; }

        public bool ChildTooOld { get; set; }

    }
}