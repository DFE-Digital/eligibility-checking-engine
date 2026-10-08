using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class HashGateway : IHash
{
    protected readonly IAudit _audit;
    private readonly IEligibilityCheckContext _db;
    private readonly int _hashCheckDays;
    private readonly int _hashCheckDaysWF;

    private readonly ILogger _logger;

    /// <summary>
    ///     Manages Check Hashing
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="dbContext"></param>
    /// <param name="configuration"></param>
    /// <param name="audit"></param>
    public HashGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IConfiguration configuration,
        IAudit audit)
    {
        _logger = logger.CreateLogger("HashService");
        _db = dbContext;
        _hashCheckDays = configuration.GetValue<short>("HashCheckDays");
        _hashCheckDaysWF = configuration.GetValue<short>("HashCheckDaysWF");
        _audit = audit;
    }

    /// <summary>
    ///     does a hash item exist for a check
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public async Task<EligibilityCheckHash?> Exists(CheckProcessData item)
    {
        var hashValidityDays = item.Type == CheckEligibilityType.WorkingFamilies ? _hashCheckDaysWF : _hashCheckDays;
        var age = DateTime.UtcNow.AddDays(-hashValidityDays);
        var hash = item.GetHash();
        return await _db.EligibilityCheckHashes.FirstOrDefaultAsync(x => x.Hash == hash && x.TimeStamp >= age);
    }

    /// <summary>
    ///     Batched version of <see cref="Exists" />. See interface doc comment (<see cref="IHash.ExistsBatch" />)
    ///     for the full rationale (ELIG-3354 / docs/bulk-check-hash-batching-fix.md).
    /// </summary>
    public async Task<Dictionary<string, EligibilityCheckHash>> ExistsBatch(IEnumerable<CheckProcessData> items, CheckEligibilityType type)
    {
        var hashValidityDays = type == CheckEligibilityType.WorkingFamilies ? _hashCheckDaysWF : _hashCheckDays;
        var age = DateTime.UtcNow.AddDays(-hashValidityDays);

        // Compute every record's hash in memory first (cheap, no DB access), then issue ONE
        // "WHERE Hash IN (...)" query for the WHOLE batch, instead of the old one-query-per-record
        // loop (which was doing thousands of sequential awaited round-trips for large batches).
        var hashes = items.Select(i => i.GetHash()).Distinct().ToList();
        if (hashes.Count == 0)
        {
            return new Dictionary<string, EligibilityCheckHash>();
        }

        var matches = await _db.EligibilityCheckHashes
            .Where(x => hashes.Contains(x.Hash) && x.TimeStamp >= age)
            .ToListAsync();

        // A hash string could (rarely) match more than one stored row within the validity window.
        // Exists() above just returns whatever FirstOrDefaultAsync() happens to give back; here we
        // deliberately prefer the most recent row per hash so the batched result is deterministic.
        return matches
            .GroupBy(x => x.Hash)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.TimeStamp).First());
    }

    /// <summary>
    ///     Create the hash item and audit
    /// </summary>
    /// <param name="item"></param>
    /// <param name="outcome"></param>
    /// <param name="tier"></param>
    /// <param name="source"></param>
    /// <param name="auditDataTemplate"></param>
    /// <returns></returns>
    /// <remarks>NOTE there is no save, Context should be saved in calling service</remarks>
    public async Task<string> Create(CheckProcessData item, CheckEligibilityStatus outcome, EligibilityTier? tier,
        ProcessEligibilityCheckSource source,EligibilityCheckContext dbContextFactory = null )
    {
        var hash = item.GetHash();

        var HashItem = new EligibilityCheckHash
        {
            EligibilityCheckHashID = Guid.NewGuid().ToString(),

            Hash = hash,
            Type = item.Type,
            Outcome = outcome,
            Tier = tier,
            TimeStamp = DateTime.UtcNow,
            Source = source
        };
        var context = dbContextFactory ?? _db;
        await context.EligibilityCheckHashes.AddAsync(HashItem);
        return HashItem.EligibilityCheckHashID;
    }
}