namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityBulkResponse
{
    public IEnumerable<CheckEligibilityItemBase> Data { get; set; }
}