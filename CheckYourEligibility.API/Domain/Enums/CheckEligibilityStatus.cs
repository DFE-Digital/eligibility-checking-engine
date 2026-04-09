// Ignore Spelling: Fsm

namespace CheckYourEligibility.API.Domain.Enums;

/// <summary>
/// Represents the status of an eligibility check
/// </summary>
public enum CheckEligibilityStatus
{
    /// <summary>
    /// The check is queued for processing
    /// </summary>
    queuedForProcessing,
    
    /// <summary>
    /// The parent was not found
    /// </summary>
    parentNotFound,
    
    /// <summary>
    /// The applicant is eligible
    /// </summary>
    eligible,
    
    /// <summary>
    /// The applicant is not eligible
    /// </summary>
    notEligible,
    
    /// <summary>
    /// An error occurred during processing
    /// </summary>
    error,
    
    /// <summary>
    /// The record was not found
    /// </summary>
    notFound,
    
    /// <summary>
    /// This is now redundant
    /// but we want to keep it for historical records.
    /// We now use IsDeleted flag to indicate if a record is soft deleted, so this status is not used for new records.    
    /// </summary>
    deleted
}