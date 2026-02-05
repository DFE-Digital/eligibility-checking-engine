namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityBulkStatusResponse
{
    public BulkStatus Data { get; set; }
    public BulkCheckResponseLinks Links { get; set; }
}


public class CheckEligibilityBulkItemResponse
{
    public IEnumerable<BulkCheck> Data { get; set; }
}

public class BulkCheckResponseLinks
{
    public string Get_BulkCheck_Results { get; set; }
}

public class BulkStatus
{
    public int Total { get; set; }
    public int Complete { get; set; }
}

public class BulkCheck : BulkCheckResponseLinks
{
    public string Id { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public string EligibilityType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Filename {  get; set; } = string.Empty;
    public string SubmittedBy {  get; set; } = string.Empty;
    public string FinalNameInCheck { get; set; } = string.Empty;
    public int NumberOfRecords { get; set; }
}

public class CheckEligibilityBulkStatusesResponse
{
    public IEnumerable<BulkCheck> Checks { get; set; }
}