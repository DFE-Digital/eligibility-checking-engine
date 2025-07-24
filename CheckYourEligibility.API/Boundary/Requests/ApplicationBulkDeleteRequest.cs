using Microsoft.AspNetCore.Http;

namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Request for bulk deleting applications from a CSV or JSON file
/// </summary>
public class ApplicationBulkDeleteRequest
{
    /// <summary>
    /// The CSV or JSON file containing application GUIDs to delete
    /// </summary>
    public required IFormFile File { get; set; }
}
