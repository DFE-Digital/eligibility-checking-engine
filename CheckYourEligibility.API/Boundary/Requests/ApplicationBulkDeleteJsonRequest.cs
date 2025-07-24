namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Request for bulk deleting applications from JSON body data
/// </summary>
public class ApplicationBulkDeleteJsonRequest
{
    /// <summary>
    /// List of application GUIDs to delete
    /// </summary>
    public required List<string> ApplicationGuids { get; set; }
}
