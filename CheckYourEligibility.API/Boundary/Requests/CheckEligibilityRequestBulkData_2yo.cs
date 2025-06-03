namespace CheckYourEligibility.API.Boundary.Requests;

public class CheckEligibilityRequestBulkData_2yo : CheckEligibilityRequestData_2yo
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequestBulk_2yo : ICheckEligibilityBulkRequest<CheckEligibilityRequestBulkData_2yo>
{
    public IEnumerable<CheckEligibilityRequestBulkData_2yo> Data { get; set; }
}
