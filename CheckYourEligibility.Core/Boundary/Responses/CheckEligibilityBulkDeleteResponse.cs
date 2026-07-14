namespace CheckYourEligibility.Core.Boundary.Responses
{
    public class CheckEligibilityBulkDeleteResponse
    {
        public CheckEligibilityBulkDeleteResponseData Data { get; set; }
    }
    
    public class CheckEligibilityBulkDeleteResponseData
    {
        public string Id { get; set; }
        public string Status { get; set; }
    }
}