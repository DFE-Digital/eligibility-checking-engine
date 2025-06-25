namespace CheckYourEligibility.API.Boundary.Responses
{
    public class CheckEligibilityBulkDeleteResponse
    {
        public string GroupId { get; set; }
        public int DeletedCount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
