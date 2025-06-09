// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Request for bulk importing applications from a CSV or JSON file
/// </summary>
public class ApplicationBulkImportRequest
{
    /// <summary>
    /// The CSV or JSON file containing application data
    /// </summary>
    public required IFormFile File { get; set; }
}

/// <summary>
/// Data structure representing a single application record for bulk import
/// </summary>
public class ApplicationBulkImportData
{
    /// <summary>
    /// Parent's first name
    /// </summary>
    public required string ParentFirstName { get; set; }
    
    /// <summary>
    /// Parent's surname
    /// </summary>
    public required string ParentSurname { get; set; }
    
    /// <summary>
    /// Parent's date of birth
    /// </summary>
    public required string ParentDateOfBirth { get; set; }
    
    /// <summary>
    /// Parent's National Insurance Number
    /// </summary>
    public required string ParentNino { get; set; }
    
    /// <summary>
    /// Parent's email address
    /// </summary>
    public required string ParentEmail { get; set; }
    
    /// <summary>
    /// Child's first name
    /// </summary>
    public required string ChildFirstName { get; set; }
    
    /// <summary>
    /// Child's surname
    /// </summary>
    public required string ChildSurname { get; set; }
    
    /// <summary>
    /// Child's school URN
    /// </summary>
    public required string ChildSchoolUrn { get; set; }
    
    /// <summary>
    /// Eligibility end date
    /// </summary>
    public required string EligibilityEndDate { get; set; }
}
