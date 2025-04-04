namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityBulkStatusResponse
{
    public BulkStatus Data { get; set; } = new();
    public BulkCheckResponseLinks Links { get; set; } = new();
}

public class BulkCheckResponseLinks
{
    public string Get_BulkCheck_Results { get; set; } = string.Empty;
}

public class BulkStatus
{
    public int Total { get; set; }
    public int Complete { get; set; }
}