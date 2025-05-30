namespace CheckYourEligibility.API.Boundary.Requests;

public class CheckEligibilityRequestBulkData_Eypp : CheckEligibilityRequestData_Eypp
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequestBulk_Eypp : ICheckEligibilityBulkRequest<CheckEligibilityRequestBulkData_Eypp>
{
    public IEnumerable<CheckEligibilityRequestBulkData_Eypp> Data { get; set; }
}