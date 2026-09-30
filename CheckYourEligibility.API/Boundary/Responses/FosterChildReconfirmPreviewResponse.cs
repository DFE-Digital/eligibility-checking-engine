namespace CheckYourEligibility.API.Boundary.Responses
{
    public class FosterChildReconfirmPreviewResponse
    {

        public string ChildFullName { get; init; }

        public DateTime ValidityStartDate { get; init; }

        public Term ValidFromTerm { get; init; }

        public DateTime ReconfirmBetweenStart { get; init; }

        public DateTime ReconfirmBetweenEnd { get; init; }

        public DateTime GracePeriodEndDate { get; init; }
    }
}