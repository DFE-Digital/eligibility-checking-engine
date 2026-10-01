namespace CheckYourEligibility.API.Boundary.Responses
{
    public class FosterCodePreviewResponse
    {
        public DateTime ValidityStartDate { get; init; }

        public Term ValidFromTerm { get; init; }

        public DateTime ReconfirmBetweenStart { get; init; }
        
        public DateTime ReconfirmBetweenEnd { get; init; }
        
        public DateTime GracePeriodEndDate { get; init; }
    }
}