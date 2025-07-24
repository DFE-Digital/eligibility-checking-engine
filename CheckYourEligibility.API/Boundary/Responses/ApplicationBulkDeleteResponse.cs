namespace CheckYourEligibility.API.Boundary.Responses;

/// <summary>
/// Response for bulk delete operations
/// </summary>
public class ApplicationBulkDeleteResponse
{
    /// <summary>
    /// Summary message of the operation
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of records processed
    /// </summary>
    public int TotalRecords { get; set; }
    
    /// <summary>
    /// Number of successful deletions
    /// </summary>
    public int SuccessfulDeletions { get; set; }
    
    /// <summary>
    /// Number of failed deletions
    /// </summary>
    public int FailedDeletions { get; set; }
    
    /// <summary>
    /// List of errors encountered during the operation
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();
}
