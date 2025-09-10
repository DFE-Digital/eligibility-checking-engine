namespace CheckYourEligibility.API.Domain.Enums;

/// <summary>
/// Represents the status of a bulk check operation
/// </summary>
public enum BulkCheckStatus
{
    /// <summary>
    /// The bulk check is currently in progress
    /// </summary>
    InProgress,
    
    /// <summary>
    /// The bulk check has completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// The bulk check has failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// The bulk check has been cancelled
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// The bulk check has been deleted (soft delete)
    /// </summary>
    Deleted
}
