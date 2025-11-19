// Ignore Spelling: Fsm

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways;

public class BulkCheckGateway : BaseGateway, IBulkCheck
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
            .Where(x => x.BulkCheckID == guid && x.Status != CheckEligibilityStatus.deleted).ToList();
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
            .Where(x => x.BulkCheckID == guid && x.Status != CheckEligibilityStatus.deleted)
            .GroupBy(n => n.Status)
            .Select(n => new { Status = n.Key, ct = n.Count() });
        if (results.Any())
            return new BulkStatus
            {
                Total = results.Sum(s => s.ct),
                Complete = results.Where(a => a.Status != CheckEligibilityStatus.queuedForProcessing).Sum(s => s.ct)
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

        // Execute query with optimized includes and projections
        var bulkChecks = await query
            .Include(x => x.EligibilityChecks)
            .OrderByDescending(x => x.SubmittedDate) // Add ordering for consistent results and better UX
            .ToListAsync();

        // Optimize status calculation using LINQ for better performance
        foreach (var bulkCheck in bulkChecks)
        {
            if (bulkCheck.EligibilityChecks?.Any() == true)
            {
                // Filter out deleted eligibility checks for status calculation
                var eligibilityStatuses = bulkCheck.EligibilityChecks
                    .Where(x => x.Status != CheckEligibilityStatus.deleted)
                    .Select(x => x.Status).ToList();

                // Use more efficient status checking
                var queuedCount = eligibilityStatuses.Count(s => s == CheckEligibilityStatus.queuedForProcessing);
                var errorCount = eligibilityStatuses.Count(s => s == CheckEligibilityStatus.error);
                var totalCount = eligibilityStatuses.Count;

                // Calculate expected status more efficiently
                BulkCheckStatus expectedStatus;
                if (totalCount == 0)
                {
                    // All records deleted - maintain current status or set to Deleted
                    bulkCheck.Status = BulkCheckStatus.Deleted;
                }
                else if (queuedCount > 0)
                {
                    bulkCheck.Status = BulkCheckStatus.InProgress; // Queued
                }
                else if (queuedCount == 0)
                {
                    bulkCheck.Status = BulkCheckStatus.Completed; // All completed
                }
                else
                {
                    bulkCheck.Status = BulkCheckStatus.InProgress; // Partially completed
                }
            }
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