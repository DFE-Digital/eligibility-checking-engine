namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Request for bulk importing applications from JSON body data
/// </summary>
public class ApplicationBulkImportJsonRequest
{
    /// <summary>
    /// List of application data for bulk import
    /// </summary>
    public required List<ApplicationBulkImportData> Applications { get; set; }
}