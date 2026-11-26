namespace CheckYourEligibility.Core.Domain.Enums;

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
    Deleted,

    /// <summary>
    /// Application creation from this bulk check is currently in progress
    /// </summary>
    ApplicationCreationInProgress,

    /// <summary>
    /// Applications have been created from this bulk check
    /// </summary>
    ApplicationsCreated,

    /// <summary>
    /// Application creation from this bulk check failed
    /// </summary>
    ApplicationCreationFailed
}