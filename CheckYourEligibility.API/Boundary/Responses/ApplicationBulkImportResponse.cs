namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationBulkImportResponse
{
    public string Message { get; set; }
    public int TotalRecords { get; set; }
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
