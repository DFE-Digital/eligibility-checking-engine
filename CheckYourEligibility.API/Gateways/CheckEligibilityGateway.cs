using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace CheckYourEligibility.API.Gateways;

public class CheckEligibilityGateway : ICheckEligibility
{
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;

    private readonly IHash _hashGateway;
    private readonly IStorageQueue _storageQueueGateway;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;
    private string _groupId;

    public CheckEligibilityGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        IConfiguration configuration, IHash hashGateway, IStorageQueue storageQueueGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _mapper = mapper;
        _hashGateway = hashGateway;
        _storageQueueGateway = storageQueueGateway;
        _configuration = configuration;
    }

    public async Task PostCheck<T>(T data, string groupId, CheckMetaData meta) where T : IEnumerable<IEligibilityServiceType>
    {
        _groupId = groupId;

        // IMPORTANT: the entire method body below - mapping/hashing (MapChecksBulk) AND the
        // insert/queue-send - is wrapped in ONE try/catch. Previously only the insert+queue-send
        // part was protected here; an exception thrown while mapping/hashing an individual record
        // escaped this method entirely, propagated up to the caller's fire-and-forget Task.Run
        // (CheckEligibilityBulkUseCase), and was only ever logged there. The BulkCheck row was left
        // at Status=InProgress FOREVER with no error ever raised against it, because nothing here
        // ever got the chance to mark it Failed. See docs/bulk-check-hash-batching-fix.md for the
        // incident this closes.
        try
        {
            // Map + resolve hash-cache hits for the WHOLE batch using a small, fixed number of
            // batched DB round-trips, instead of the old per-record loop (up to 4 SEQUENTIAL
            // awaited DB calls PER RECORD - e.g. up to 18,000 round-trips for a 4,500-record
            // batch). See MapChecksBulk below - this is the fix for Jira ELIG-3354.
            var mappedBulkedChecks = await MapChecksBulk(data, meta);

            // Insert all rows into the DB BEFORE sending any queue messages,
            // so the engine never processes a message for a row that doesn't exist yet.
            _db.BulkInsert_EligibilityCheck(mappedBulkedChecks);

            // Now send queue messages for records that weren't resolved from the hash cache.
            // Reuse a single QueueClient for all bulk messages — they share the same queue.
            var queuedBulkItems = mappedBulkedChecks.Where(x => x.Status == CheckEligibilityStatus.queuedForProcessing).ToList();

            if (queuedBulkItems.Any())
            {

                string bulkQueueName = GetBulkQueueName(queuedBulkItems.First().Type, meta.Source);


                foreach (var item in queuedBulkItems)
                {
                    await _storageQueueGateway.SendMessage(item, bulkQueueName);
                }
            }
            else
            {
                // Every record in this batch was resolved straight from the hash cache - nothing
                // was queued, so the whole BulkCheck is already complete.
                await MarkBulkCheckCompleted(groupId);
            }
        }
        catch (Exception e)
        {

            _logger.LogError(e,
                    "Bulk insert failed for BulkCheck {BulkCheckId}",
                    groupId);

            // Always mark the BulkCheck as Failed on ANY failure above (mapping/hashing OR
            // insert/queue-send), so it can never be left stuck at InProgress forever with zero
            // signal to the caller or to support staff investigating it.
            await MarkBulkCheckFailed(groupId);

            throw;

        }


    }

    /// <summary>
    ///     Marks a BulkCheck as Completed and logs the standard "{BulkCheckEvent}" completion event.
    /// </summary>
    private async Task MarkBulkCheckCompleted(string groupId)
    {
        var bulkCheck = await _db.BulkChecks.FirstOrDefaultAsync(x => x.BulkCheckID == groupId);
        if (bulkCheck == null)
        {
            return;
        }

        bulkCheck.Status = BulkCheckStatus.Completed;
        bulkCheck.CompletedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        LogBulkCheckEvent(bulkCheck);
    }

    /// <summary>
    ///     Marks a BulkCheck as Failed and logs the standard "{BulkCheckEvent}" event. Any secondary
    ///     failure while doing this is itself only logged (never re-thrown), so a problem updating
    ///     the status can never mask/replace the original exception the caller is about to re-throw.
    /// </summary>
    private async Task MarkBulkCheckFailed(string groupId)
    {
        try
        {
            var bulkCheck = await _db.BulkChecks.FirstOrDefaultAsync(x => x.BulkCheckID == groupId);
            if (bulkCheck == null)
            {
                return;
            }

            bulkCheck.Status = BulkCheckStatus.Failed;
            bulkCheck.CompletedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            LogBulkCheckEvent(bulkCheck);
        }
        catch (Exception statusUpdateEx)
        {
            _logger.LogError(statusUpdateEx,
                "Unable to mark BulkCheck {BulkCheckId} as Failed",
                groupId);
        }
    }

    /// <summary>
    ///     Logs the standard structured "{BulkCheckEvent}" completion/failure event for a BulkCheck.
    /// </summary>
    private void LogBulkCheckEvent(Domain.BulkCheck bulkCheck)
    {
        var elapsedTime = bulkCheck.CompletedDate.Value - bulkCheck.SubmittedDate;

        var logEvent = JsonConvert.SerializeObject(new
        {
            BulkCheckId = bulkCheck.BulkCheckID,
            Status = bulkCheck.Status.ToString(),
            SubmittedDate = bulkCheck.SubmittedDate,
            CompletedDate = bulkCheck.CompletedDate,
            ElapsedMilliseconds = elapsedTime.TotalMilliseconds,
            NumberOfRecords = bulkCheck.NumberOfRecords,
            OrganisationID = bulkCheck.OrganisationID
        });

        _logger.LogInformation("{BulkCheckEvent}", logEvent);
    }

    /// <summary>
    ///     A record awaiting its "prior check data" to be copied across from an earlier check that
    ///     resolved to the same hash. Collected during <see cref="MapChecksBulk" /> so the lookup can
    ///     be done in a handful of BATCHED queries afterwards, instead of one query (or up to four,
    ///     with retries) per record.
    /// </summary>
    private sealed record PendingHashCopy(EligibilityCheck Item, CheckProcessData CheckData, string HashId, CheckEligibilityStatus Status);

    /// <summary>
    ///     Maps a whole batch of bulk-submitted records to <see cref="EligibilityCheck" /> rows and
    ///     resolves hash-cache hits for the WHOLE BATCH using a small, fixed number of batched DB
    ///     round-trips, instead of the old per-record loop (1 hash lookup + up to 3 retried "find
    ///     prior check data" lookups PER RECORD = up to 4 sequential awaited DB calls per record).
    ///     For a 4,500-record batch that's the difference between roughly 5-8 total DB round-trips
    ///     and up to 18,000 - see docs/bulk-check-hash-batching-fix.md and Jira ELIG-3354 for the
    ///     full incident this fixes. This method does NOT insert anything into the database, so it's
    ///     safe to await in full before the insert/queue-send step in <see cref="PostCheck{T}(T, string, CheckMetaData)" />.
    ///     Internal (rather than private) purely so unit tests can exercise the batching logic
    ///     directly without going through <c>BulkInsert_EligibilityCheck</c>, which the EF Core
    ///     InMemory provider used in tests can't execute.
    /// </summary>
    internal async Task<List<EligibilityCheck>> MapChecksBulk(IEnumerable<IEligibilityServiceType> data, CheckMetaData meta)
    {
        var items = new List<EligibilityCheck>();
        var checkDataByItem = new Dictionary<EligibilityCheck, CheckProcessData>();

        // Pass 1: pure in-memory mapping - identical field-by-field logic to the single-record
        // MapCheck() below, but with NO DB access at all, so this whole pass is effectively
        // instant regardless of batch size.
        foreach (var d in data)
        {
            var item = _mapper.Map<EligibilityCheck>(d);
            var baseType = d as CheckEligibilityRequestDataBase;

            item.CheckData = JsonConvert.SerializeObject(d);
            item.Type = baseType.Type;
            item.BulkCheckID = _groupId;
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.Created = DateTime.UtcNow;
            item.Updated = DateTime.UtcNow;
            item.Status = CheckEligibilityStatus.queuedForProcessing;

            if (meta != null)
            {
                item.OrganisationID = meta.OrganisationID;
                item.OrganisationType = !string.IsNullOrEmpty(meta.OrganisationType) ? meta.OrganisationType : null;
                item.Source = meta.Source;
                item.UserName = meta.UserName;
            }

            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData);
            items.Add(item);
            checkDataByItem[item] = checkData;
        }

        if (items.Count == 0)
        {
            return items;
        }

        // Pass 2: ONE batched hash lookup covering the entire batch, instead of one DB call per
        // record (the single biggest source of round-trips in the old code).
        var hashMatches = await _hashGateway.ExistsBatch(checkDataByItem.Values, items[0].Type);

        // Pass 3: apply hash results in memory, and collect the (much smaller) subset of records
        // that also need their "prior check data" copied across - that subset still needs DB
        // access, but it's batched too (see ApplyPriorCheckDataBatch).
        var pendingWorkingFamiliesCopy = new List<PendingHashCopy>();
        var pendingFreeSchoolMealsCopy = new List<PendingHashCopy>();

        foreach (var item in items)
        {
            var checkData = checkDataByItem[item];
            if (!hashMatches.TryGetValue(checkData.GetHash(), out var checkHashResult) || checkHashResult == null)
            {
                continue;
            }

            var hashedStatus = checkHashResult.Outcome;
            item.Status = hashedStatus;
            item.Tier = checkHashResult.Tier;
            item.EligibilityCheckHashID = checkHashResult.EligibilityCheckHashID;
            item.EligibilityCheckHash = checkHashResult;

            var isResolved = hashedStatus == CheckEligibilityStatus.eligible || hashedStatus == CheckEligibilityStatus.notEligible;
            if (!isResolved)
            {
                continue;
            }

            var pending = new PendingHashCopy(item, checkData, checkHashResult.EligibilityCheckHashID, hashedStatus);

            // Find check data of last hashed result for Working Families (retried, since the
            // referenced hash's CheckEligibilities row may not have committed yet).
            if (item.Type == CheckEligibilityType.WorkingFamilies)
            {
                pendingWorkingFamiliesCopy.Add(pending);
            }
            // Find check data of last hashed result for FSM to preserve EligibilityEndDate (single
            // one-shot lookup only - no retry, matching the original behaviour).
            else if (item.Type == CheckEligibilityType.FreeSchoolMeals)
            {
                pendingFreeSchoolMealsCopy.Add(pending);
            }
        }

        if (pendingWorkingFamiliesCopy.Count > 0)
        {
            await ApplyPriorCheckDataBatch(pendingWorkingFamiliesCopy, maxAttempts: 3, retryDelay: TimeSpan.FromSeconds(1));
        }

        if (pendingFreeSchoolMealsCopy.Count > 0)
        {
            await ApplyPriorCheckDataBatch(pendingFreeSchoolMealsCopy, maxAttempts: 1, retryDelay: TimeSpan.Zero);
        }

        return items;
    }

    /// <summary>
    ///     Batched replacement for the per-record "find the most recent prior check with this hash
    ///     and copy its CheckData across" loop that used to live inline in MapCheck(). Looks up all
    ///     still-outstanding EligibilityCheckHashIDs in ONE query per attempt (instead of one query
    ///     PER RECORD), retrying only the subset still missing after each attempt - so a 3-attempt
    ///     retry over 1,000 WorkingFamilies hash-hits is ~3 queries + up to 2 one-second delays
    ///     TOTAL, not up to 3,000 queries and up to 3,000 seconds of delay.
    ///     Mirrors the original per-record try/catch: any failure here is logged and swallowed
    ///     (NOT re-thrown), so a problem with this purely-cosmetic enrichment step can never fail
    ///     the whole batch - affected records simply keep their own freshly-submitted CheckData.
    /// </summary>
    private async Task ApplyPriorCheckDataBatch(List<PendingHashCopy> pending, int maxAttempts, TimeSpan retryDelay)
    {
        var remaining = pending;

        try
        {
            for (var attempt = 1; attempt <= maxAttempts && remaining.Count > 0; attempt++)
            {
                var hashIds = remaining.Select(p => p.HashId).Distinct().ToList();

                // ONE query covering every still-outstanding HashID in this attempt. Materialize
                // first (ToListAsync), then do the "most recent per hash" grouping in memory -
                // keeps this provider-agnostic (works the same against SQL Server and the EF Core
                // InMemory provider used in tests, which can't translate GroupBy to SQL).
                var latestPerHashId = (await _db.CheckEligibilities
                        .Where(x => hashIds.Contains(x.EligibilityCheckHashID))
                        .OrderByDescending(x => x.Created)
                        .AsNoTracking()
                        .ToListAsync())
                    .GroupBy(x => x.EligibilityCheckHashID)
                    .ToDictionary(g => g.Key, g => g.First());

                var stillMissing = new List<PendingHashCopy>();

                foreach (var item in remaining)
                {
                    if (latestPerHashId.TryGetValue(item.HashId, out var firstValidCheck) && firstValidCheck.Status == item.Status)
                    {
                        var hashCheckData = JsonConvert.DeserializeObject<CheckProcessData>(firstValidCheck.CheckData);
                        hashCheckData.ClientIdentifier = item.CheckData.ClientIdentifier;
                        hashCheckData.FirstName = item.CheckData.FirstName;
                        hashCheckData.ChildFirstName = item.CheckData.ChildFirstName;
                        hashCheckData.ChildLastName = item.CheckData.ChildLastName;
                        hashCheckData.ChildDateOfBirth = item.CheckData.ChildDateOfBirth;
                        hashCheckData.ChildSchoolURN = item.CheckData.ChildSchoolURN;
                        hashCheckData.EmailAddress = item.CheckData.EmailAddress;
                        item.Item.CheckData = JsonConvert.SerializeObject(hashCheckData);

                        _logger.LogInformation($"Action: Retrieve check with HashID:{item.HashId}, Status:Found, Attempt:{attempt}");
                    }
                    else
                    {
                        stillMissing.Add(item);
                    }
                }

                remaining = stillMissing;

                if (remaining.Count > 0 && attempt < maxAttempts)
                {
                    _logger.LogWarning($"Action: Retrieve check batch, Status:NotFound for {remaining.Count} record(s), Attempt:{attempt}");
                    await Task.Delay(retryDelay);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving cached check data for {remaining.Count} record(s)");
        }
    }

    public async Task<PostCheckResult> PostCheck<T>(T data, CheckMetaData meta) where T : IEligibilityServiceType
    {

        var item = await MapCheck(data, meta);
        await _db.CheckEligibilities.AddAsync(item);
        await _db.SaveChangesAsync();

        // Send queue message after the row is committed to the DB.
        if (item.Status == CheckEligibilityStatus.queuedForProcessing)
        {
            var singleQueueName = _configuration[$"Queue:Single:{item.Type}"];
            await _storageQueueGateway.SendMessage(item, singleQueueName);
        }

        return new PostCheckResult { Id = item.EligibilityCheckID, Status = item.Status, Tier = item.Tier };

    }
    public async Task<EligibilityCheck> MapCheck<T>(T data, CheckMetaData meta) where T : IEligibilityServiceType
    {
        var item = _mapper.Map<EligibilityCheck>(data);

        try
        {

            var baseType = data as CheckEligibilityRequestDataBase;           

            item.CheckData = JsonConvert.SerializeObject(data);          

            item.Type = baseType.Type;

            item.BulkCheckID = _groupId;
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.Created = DateTime.UtcNow;
            item.Updated = DateTime.UtcNow;
            item.Status = CheckEligibilityStatus.queuedForProcessing;

            if (meta != null)
            {
                item.OrganisationID = meta.OrganisationID;
                item.OrganisationType = !string.IsNullOrEmpty(meta.OrganisationType) ? meta.OrganisationType : null;
                item.Source = meta.Source;
                item.UserName = meta.UserName;
            }
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData);

            //TODO: The hashing logic should sit in the use case, targeting the hash gateway
            var checkHashResult =
                await _hashGateway.Exists(checkData);
            if (checkHashResult != null)
            {

                CheckEligibilityStatus hashedStatus = checkHashResult.Outcome;
                item.Status = hashedStatus;
                item.Tier = checkHashResult.Tier;
                item.EligibilityCheckHashID = checkHashResult.EligibilityCheckHashID;
                item.EligibilityCheckHash = checkHashResult;

                // Find check data of last hashed result for Working families
                if (data.Type == CheckEligibilityType.WorkingFamilies && (hashedStatus == CheckEligibilityStatus.eligible || hashedStatus == CheckEligibilityStatus.notEligible))
                {
                    try
                    {

                        for (int i = 1; i <= 3; i++)
                        {

                            var firstValidCheck = await _db.CheckEligibilities
                           .Where(x => x.EligibilityCheckHashID == checkHashResult.EligibilityCheckHashID &&
                                       x.Status == hashedStatus).OrderByDescending(x => x.Created).AsNoTracking().FirstOrDefaultAsync();
                            if (firstValidCheck != null)
                            {

                                CheckProcessData hashCheckData = JsonConvert.DeserializeObject<CheckProcessData>(firstValidCheck.CheckData);
                                hashCheckData.ClientIdentifier = checkData.ClientIdentifier;
                                hashCheckData.FirstName = checkData.FirstName;
                                hashCheckData.ChildFirstName = checkData.ChildFirstName;
                                hashCheckData.ChildLastName = checkData.ChildLastName;
                                hashCheckData.ChildDateOfBirth = checkData.ChildDateOfBirth;
                                hashCheckData.ChildSchoolURN = checkData.ChildSchoolURN;
                                hashCheckData.EmailAddress = checkData.EmailAddress;
                                item.CheckData = JsonConvert.SerializeObject(hashCheckData);
                                _logger.LogInformation($"Action: Retrieve check with HashID:{checkHashResult.EligibilityCheckHashID}, Status:Found, Attempt:{i} ");
                                break;

                            }
                            _logger.LogWarning($"Action: Retrieve check with HashID:{checkHashResult.EligibilityCheckHashID}, Status:NotFound, Attempt:{i} ");
                            await Task.Delay(1000);
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error creating check with ID: {item.EligibilityCheckHashID}");
                    }
                }
                // Find check data of last hashed result for FSM to preserve EligibilityEndDate
                else if (data.Type == CheckEligibilityType.FreeSchoolMeals && (hashedStatus == CheckEligibilityStatus.eligible || hashedStatus == CheckEligibilityStatus.notEligible))
                {
                    try
                    {
                        var firstValidCheck = await _db.CheckEligibilities
                            .Where(x => x.EligibilityCheckHashID == checkHashResult.EligibilityCheckHashID &&
                                        x.Status == hashedStatus)
                            .OrderByDescending(x => x.Created)
                            .AsNoTracking()
                            .FirstOrDefaultAsync();

                        if (firstValidCheck != null)
                        {
                            CheckProcessData hashCheckData = JsonConvert.DeserializeObject<CheckProcessData>(firstValidCheck.CheckData);
                            hashCheckData.ClientIdentifier = checkData.ClientIdentifier;
                            hashCheckData.FirstName = checkData.FirstName;
                            hashCheckData.ChildFirstName = checkData.ChildFirstName;
                            hashCheckData.ChildLastName = checkData.ChildLastName;
                            hashCheckData.ChildDateOfBirth = checkData.ChildDateOfBirth;
                            hashCheckData.ChildSchoolURN = checkData.ChildSchoolURN;
                            hashCheckData.EmailAddress = checkData.EmailAddress;
                            item.CheckData = JsonConvert.SerializeObject(hashCheckData);
                            _logger.LogInformation($"Action: Retrieve check with HashID:{checkHashResult.EligibilityCheckHashID}, Status:Found");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error retrieving cached check data with ID: {item.EligibilityCheckHashID}");
                    }
                }
            }
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Db post");
            throw;
        }
    }

    public async Task<(CheckEligibilityStatus?, EligibilityTier?, string?)> GetStatusAsync(
        string guid,
        CheckEligibilityType type)
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x =>
            x.EligibilityCheckID == guid &&
            (type == CheckEligibilityType.None || type == x.Type) &&
            x.IsDeleted == false);

        if (result != null)
        {
            var checkData = string.IsNullOrWhiteSpace(result.CheckData)
                ? null
                : JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);

            return (result.Status, result.Tier, checkData?.ErrorCode);
        }

        return (null, null, null);
    }

    public async Task<CheckEligibilityBulkDeleteResponseData> DeleteByBulkCheckId(string bulkCheckId)
    {
        if (string.IsNullOrEmpty(bulkCheckId)) throw new ValidationException(null, "Invalid Request, group ID is required.");

        var response = new CheckEligibilityBulkDeleteResponseData
        {
            Id = bulkCheckId,
        };

        try
        {
            _logger.LogInformation($"Attempting to soft delete EligibilityChecks and BulkCheck for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");
            var bulkCheckLimit = _configuration.GetValue<int>("BulkEligibilityCheckLimit");

            var records = await _db.CheckEligibilities
                .Where(x => x.BulkCheckID == bulkCheckId)
                .ToListAsync();

            if (!records.Any())
            {
                _logger.LogWarning(
                    $"Bulk upload with ID {bulkCheckId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found or already deleted");
                throw new NotFoundException(bulkCheckId);
            }

            // Soft delete the EligibilityCheck records by setting IsDeleted to true, and updating the Updated timestamp
            foreach (var record in records)
            {
                if (record.Status == CheckEligibilityStatus.queuedForProcessing)
                {
                    record.Status = CheckEligibilityStatus.deleted;
                }

                record.IsDeleted = true;
                record.Updated = DateTime.UtcNow;
            }

            // set bulk check record to deleted
            var bulkCheckRecord = await _db.BulkChecks.FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId);
            if (bulkCheckRecord != null)
                bulkCheckRecord.Status = BulkCheckStatus.Deleted;

            await _db.SaveChangesAsync();

            _logger.LogInformation($"Soft deleted {records.Count} EligibilityChecks and associated BulkCheck for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");

            response.Status = "Success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting EligibilityChecks for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");

            response.Status = "Error";
        }

        return response;
    }
    public async Task<T?> GetItem<T>(string guid, CheckEligibilityType type, bool isBatchRecord = false) where T : CheckEligibilityItem
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid &&
                                                                           (type == CheckEligibilityType.None ||
                                                                            type == x.Type) &&
                                                                           x.IsDeleted == false);


        var item = _mapper.Map<CheckEligibilityItem>(result);
        if (result != null)
        {
            var CheckData = GetCheckProcessData(result.Type, result.CheckData);
            if (isBatchRecord)
            {
                item.EligibilityCheckID = result.EligibilityCheckID;
                item.Status = result.Status.ToString();
                item.Created = result.Created;
                item.ClientIdentifier = CheckData.ClientIdentifier;
            }

            //TODO: This can probably be done as a map
            switch (result.Type)
            {
                case CheckEligibilityType.WorkingFamilies:
                    item.EligibilityCode = CheckData.EligibilityCode;
                    item.LastName = CheckData.LastName;
                    item.ValidityStartDate = CheckData.ValidityStartDate;
                    item.ValidityEndDate = CheckData.ValidityEndDate;
                    item.GracePeriodEndDate = CheckData.GracePeriodEndDate;
                    item.NationalInsuranceNumber = CheckData.NationalInsuranceNumber;
                    item.DateOfBirth = CheckData.DateOfBirth;
                    break;
                default:
                    item.DateOfBirth = CheckData.DateOfBirth;
                    item.NationalInsuranceNumber = CheckData.NationalInsuranceNumber;
                    item.NationalAsylumSeekerServiceNumber = CheckData.NationalAsylumSeekerServiceNumber;
                    item.LastName = CheckData.LastName;
                    item.FirstName = CheckData.FirstName;
                    item.ChildFirstName = CheckData.ChildFirstName;
                    item.ChildLastName = CheckData.ChildLastName;
                    item.ChildDateOfBirth = CheckData.ChildDateOfBirth;
                    item.ChildSchoolURN = CheckData.ChildSchoolURN;
                    item.EligibilityEndDate = CheckData.EligibilityEndDate;
                    item.EmailAddress = CheckData.EmailAddress;
                    break;
            }

            return (T)item;
        }

        return default;
    }

    public async Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid,
        EligibilityCheckStatusData data, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var result = await context.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid && x.IsDeleted == false);
        if (result != null)
        {
            result.Status = data.Status;
            result.Updated = DateTime.UtcNow;
            var updates = await context.SaveChangesAsync();
            return new CheckEligibilityStatusResponse { Data = new StatusValue { Status = result.Status.ToString() } };
        }

        return null;
    }

    public static string GetHash(CheckProcessData item)
    {
        var key = string.IsNullOrEmpty(item.NationalInsuranceNumber)
            ? item.NationalAsylumSeekerServiceNumber?.ToUpper()
            : item.NationalInsuranceNumber?.ToUpper();

        var input = $"{item.LastName?.ToUpper()}{key}{item.DateOfBirth}{item.Type}";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var inputHash = SHA256.HashData(inputBytes);
        return Convert.ToHexString(inputHash);
    }

    #region Private

    private string GetBulkQueueName(
    CheckEligibilityType type,
    string source)
    {
        return type switch
        {
            CheckEligibilityType.FreeSchoolMeals
                when source == "free-school-meals-admin"
                    => _configuration["Queue:Bulk:FreeSchoolMeals:Frontend"],

            CheckEligibilityType.FreeSchoolMeals
                    => _configuration["Queue:Bulk:FreeSchoolMeals:Api"],

            _ => _configuration[$"Queue:Bulk:{type}"]
        };
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityType type, string data)
    {
        //TODO: This should probably live with the usecase
        switch (type)
        {
            case CheckEligibilityType.FreeSchoolMeals:
            case CheckEligibilityType.TwoYearOffer:
            case CheckEligibilityType.EarlyYearPupilPremium:
                return GetCheckProcessDataType<CheckEligibilityRequestBulkData>(type, data);
            case CheckEligibilityType.WorkingFamilies:
                return GetCheckProcessDataType<CheckEligibilityRequestWorkingFamiliesBulkData>(type, data);
            default:
                throw new NotImplementedException($"Type:-{type} not supported.");
        }
    }

    private static CheckProcessData GetCheckProcessDataType<T>(CheckEligibilityType type, string data)
        where T : IEligibilityServiceType
    {
        dynamic checkItem = JsonConvert.DeserializeObject(data, typeof(T));
        switch (type)
        {
            case CheckEligibilityType.WorkingFamilies:
                return new CheckProcessData
                {
                    EligibilityCode = checkItem.EligibilityCode,
                    NationalInsuranceNumber = checkItem.NationalInsuranceNumber,
                    ValidityStartDate = checkItem.ValidityStartDate,
                    ValidityEndDate = checkItem.ValidityEndDate,
                    GracePeriodEndDate = checkItem.GracePeriodEndDate,
                    LastName = checkItem.LastName?.ToUpper(),
                    DateOfBirth = checkItem.DateOfBirth,
                    ClientIdentifier = checkItem.ClientIdentifier,
                    Type = type
                };
            default:
                return new CheckProcessData
                {
                    DateOfBirth = checkItem.DateOfBirth,
                    LastName = checkItem.LastName?.ToUpper(),
                    FirstName = checkItem.FirstName,
                    ChildFirstName = checkItem.ChildFirstName,
                    ChildLastName = checkItem.ChildLastName,
                    ChildDateOfBirth = checkItem.ChildDateOfBirth,
                    ChildSchoolURN = checkItem.ChildSchoolURN,
                    EmailAddress = checkItem.EmailAddress,
                    NationalAsylumSeekerServiceNumber = checkItem.NationalAsylumSeekerServiceNumber,
                    NationalInsuranceNumber = checkItem.NationalInsuranceNumber,
                    Type = type,
                    ClientIdentifier = checkItem.ClientIdentifier,
                    EligibilityEndDate = checkItem.EligibilityEndDate

                };
        }
    }


    #endregion
}