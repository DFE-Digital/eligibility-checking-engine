namespace CheckYourEligibility.API.Boundary.Responses
{
    public class BulkCheckSummaryResponse
    {
        public string? Filename { get; set; }

        public string? Status { get; set; }

        public DateTime SubmittedDate { get; set; }

        public string? SubmittedBy { get; set; }

        public Dictionary<string, int> Outcomes { get; set; } = new();
    }
}
