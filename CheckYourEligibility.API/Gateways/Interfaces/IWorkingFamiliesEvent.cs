using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IWorkingFamiliesEvent
{
    /// <summary>
    /// Retrieves a working families event by the HMRC-supplied identifier.
    /// </summary>
    Task<WorkingFamiliesEvent?> GetByHMRCId(string hmrcId);

    /// <summary>
    /// Creates or fully overwrites a working families event matched by HMRCEligibilityEventId.
    /// </summary>
    Task<WorkingFamiliesEvent> UpsertWorkingFamiliesEvent(WorkingFamiliesEvent data);

    /// <summary>
    /// Finds active (non-deleted) events with the same DERN but a different HMRC event ID
    /// whose validity dates overlap the given range.
    /// </summary>
    Task<List<WorkingFamiliesEvent>> GetOverlappingEventsByDern(
        string dern, string excludeHmrcId, DateTime validityStart, DateTime validityEnd);

    /// <summary>
    /// Soft-deletes a working families event (sets IsDeleted = true and records DeletedDateTime).
    /// Returns false if the event does not exist or is already deleted.
    /// </summary>
    Task<bool> DeleteWorkingFamiliesEvent(string hmrcId);
}
