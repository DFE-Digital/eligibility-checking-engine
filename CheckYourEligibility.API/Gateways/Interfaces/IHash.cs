// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IHash
{
    Task<EligibilityCheckHash?> Exists(CheckProcessData item);

    /// <summary>
    ///     Batched version of <see cref="Exists" /> - resolves hashes for a whole collection of
    ///     records in ONE database round-trip instead of one round-trip per record. Introduced to
    ///     fix bulk checks becoming extremely slow/fragile (and occasionally dying mid-batch with
    ///     zero rows persisted) when doing thousands of sequential per-record hash lookups.
    ///     See docs/bulk-check-hash-batching-fix.md and Jira ELIG-3354.
    /// </summary>
    /// <param name="items">The batch of records to resolve hashes for.</param>
    /// <param name="type">
    ///     The eligibility check type for the whole batch. Bulk submissions are always a single
    ///     type (e.g. all WorkingFamilies), so one validity-window applies to the whole batch.
    /// </param>
    /// <returns>
    ///     A dictionary keyed by hash string (as produced by <see cref="CheckProcessData.GetHash" />).
    ///     Callers should compute each record's own hash and look it up in this dictionary, since
    ///     more than one record in a batch can share the same hash (e.g. duplicate submissions).
    /// </returns>
    Task<Dictionary<string, EligibilityCheckHash>> ExistsBatch(IEnumerable<CheckProcessData> items, CheckEligibilityType type);

    Task<string> Create(CheckProcessData item, CheckEligibilityStatus checkResult, EligibilityTier? tier, ProcessEligibilityCheckSource source,
        EligibilityCheckContext dbContextFactory = null);
}