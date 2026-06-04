// Ignore Spelling: Fsm

using Azure.Storage.Queues;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways;

public class BulkCheckGateway : IBulkCheck
{
    protected readonly IAudit _audit;
    private readonly IEligibilityCheckContext _db;

    private readonly ICheckEligibility _checkEligibility;
    private readonly ILogger _logger;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;

    public BulkCheckGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext,
        ICheckEligibility checkEligibility, IAudit audit)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _checkEligibility = checkEligibility;
        _audit = audit;
    }

    public async Task<string> CreateBulkCheck(BulkCheck bulkCheck)
    {
        try
        {
            await _db.BulkChecks.AddAsync(bulkCheck);
            await _db.SaveChangesAsync();
            return bulkCheck.BulkCheckID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk check with ID: {GroupId}", bulkCheck.BulkCheckID);
            throw;
        }
    }

    public async Task<T> GetBulkCheckResults<T>(string guid) where T : IList<CheckEligibilityItem>
    {
        IList<CheckEligibilityItem> items = new List<CheckEligibilityItem>();
        var resultList = _db.CheckEligibilities
            .Where(x => x.BulkCheckID == guid && x.IsDeleted == false).ToList();
        if (resultList != null && resultList.Any())
        {
            var type = typeof(T);
            if (type == typeof(IList<CheckEligibilityItem>))
            {
                var sequence = 1;
                foreach (var result in resultList)
                {
                    var item = await _checkEligibility.GetItem<CheckEligibilityItem>(result.EligibilityCheckID, result.Type,
                        isBatchRecord: true);
                    items.Add(item);

                    sequence++;
                }

                return (T)items;
            }

            throw new Exception($"unable to cast to type {type}");
        }

        return default;
    }

    public async Task<BulkStatus?> GetBulkStatus(string guid)
    {
        var results = _db.CheckEligibilities
            .Where(x => x.BulkCheckID == guid && x.IsDeleted == false)
            .GroupBy(n => n.Status)
            .Select(n => new { Status = n.Key, ct = n.Count() });
        if (results.Any())
            return new BulkStatus
            {
                Total = results.Sum(s => s.ct),
                Complete = results.Where(a => a.Status != CheckEligibilityStatus.queuedForProcessing).Sum(s => s.ct)
            };

        // EligibilityCheck rows are inserted asynchronously after the BulkCheck record
        // is created. If none exist yet, fall back to the BulkChecks table so that
        // progress returns "0 of N complete" rather than 404.
        var bulkCheck = await _db.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == guid);
        if (bulkCheck != null)
            return new BulkStatus
            {
                Total = bulkCheck.NumberOfRecords,
                Complete = 0
            };

        return null;
    }

    /// <summary>
    /// Gets bulk check statuses for a specific local authority (optimized version)
    /// </summary>
    /// <param name="localAuthorityId">The local authority identifier to filter by</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs for the user (0 means admin access to all)</param>
    /// <param name="includeLast7DaysOnly">If true, only returns bulk checks from the last 7 days. If false, returns all non-deleted bulk checks.</param>
    /// <returns>Collection of bulk checks for the requested local authority (if user has permission)</returns>
    public async Task<IEnumerable<BulkCheck>?> GetBulkStatuses(string localAuthorityId, IList<int> allowedLocalAuthorityIds, bool includeLast7DaysOnly = true)
    {
        // Parse the requested local authority ID
        if (!int.TryParse(localAuthorityId, out var requestedLAId))
        {
            return new List<BulkCheck>();
        }

        // Build optimized query with compound filtering
        IQueryable<BulkCheck> query = _db.BulkChecks;

        // Apply date filter only if requested (for backward compatibility)
        if (includeLast7DaysOnly)
        {
            var minDate = DateTime.UtcNow.AddDays(-7);
            query = query.Where(x => x.SubmittedDate > minDate);
        }

        // Apply local authority filtering
        if (allowedLocalAuthorityIds.Contains(0))
        {
            // Admin user
            if (requestedLAId == 0)
            {
                // Special case: when requestedLAId is 0, admin wants ALL bulk checks across all local authorities
                // This is used by the new /bulk-check endpoint
                // No additional filtering needed - they can see everything
            }
            else
            {
                // Admin user requesting a specific local authority (e.g., /bulk-check/status/201)
                // Filter by the specific local authority requested
                query = query.Where(x => x.LocalAuthorityID == requestedLAId);
            }
        }
        else
        {
            // Regular user - check if they have permission for the requested LA
            if (allowedLocalAuthorityIds.Contains(requestedLAId))
            {
                // User has access to the specific requested local authority
                query = query.Where(x => x.LocalAuthorityID == requestedLAId);
            }
            else
            {
                // User doesn't have access to the requested local authority - early return
                return new List<BulkCheck>();
            }
        }

        // Load only BulkCheck metadata - do NOT include EligibilityChecks navigation property,
        // as that would load potentially 100,000+ rows into memory for large LAs.
        var bulkChecks = await query
            .OrderByDescending(x => x.SubmittedDate)
            .ToListAsync();

        if (bulkChecks.Count == 0)
            return bulkChecks;

        // Single aggregate query at the database level: count total and queued checks per BulkCheck.
        // This replaces the previous approach of loading all individual EligibilityCheck rows.
        // We count ALL rows (including soft-deleted) so we can distinguish:
        //   - no rows at all   → just submitted, treat as InProgress
        //   - rows but all deleted → Deleted
        var bulkCheckIds = bulkChecks.Select(bc => bc.BulkCheckID).ToList();
        var statusCounts = await _db.CheckEligibilities
            .Where(x => bulkCheckIds.Contains(x.BulkCheckID!))
            .GroupBy(x => x.BulkCheckID)
            .Select(g => new
            {
                BulkCheckID = g.Key,
                Total  = g.Count(x => !x.IsDeleted),
                Queued = g.Count(x => !x.IsDeleted && x.Status == CheckEligibilityStatus.queuedForProcessing),
                AnyRows = g.Count()
            })
            .ToDictionaryAsync(x => x.BulkCheckID!);

        foreach (var bulkCheck in bulkChecks)
        {
            if (!statusCounts.TryGetValue(bulkCheck.BulkCheckID, out var counts))
                // No EligibilityCheck rows at all — batch was just submitted; treat as in progress.
                bulkCheck.Status = BulkCheckStatus.InProgress;
            else if (counts.Total == 0)
                // Rows exist but all are soft-deleted.
                bulkCheck.Status = BulkCheckStatus.Deleted;
            else if (counts.Queued > 0)
                bulkCheck.Status = BulkCheckStatus.InProgress;
            else
                bulkCheck.Status = BulkCheckStatus.Completed;
        }

        return bulkChecks;
    }

    public async Task<BulkCheck?> GetBulkCheck(string guid)
    {
        var bulkCheck = await _db.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == guid);

        return bulkCheck;
    }
}