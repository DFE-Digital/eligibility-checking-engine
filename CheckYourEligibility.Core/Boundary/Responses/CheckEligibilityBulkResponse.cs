namespace CheckYourEligibility.Core.Boundary.Responses;

public class CheckEligibilityBulkResponse
{
    public IEnumerable<CheckEligibilityItemBase> Data { get; set; }
}