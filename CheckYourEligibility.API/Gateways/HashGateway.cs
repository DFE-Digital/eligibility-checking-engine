using CheckYourEligibility.API.Boundary.Requests;
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
        _audit = audit;
    }

    /// <summary>
    ///     does a hash item exist for a check
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public async Task<EligibilityCheckHash?> Exists(CheckProcessData item)
    {
        var age = DateTime.UtcNow.AddDays(-_hashCheckDays);
        var hash = item.GetHash();
        return await _db.EligibilityCheckHashes.FirstOrDefaultAsync(x => x.Hash == hash && x.TimeStamp >= age);
    }

    /// <summary>
    ///     Create the hash item and audit
    /// </summary>
    /// <param name="item"></param>
    /// <param name="outcome"></param>
    /// <param name="source"></param>
    /// <param name="auditDataTemplate"></param>
    /// <returns></returns>
    /// <remarks>NOTE there is no save, Context should be saved in calling service</remarks>
    public async Task<string> Create(CheckProcessData item, CheckEligibilityStatus outcome,
        ProcessEligibilityCheckSource source, AuditData auditDataTemplate)
    {
        var hash = item.GetHash();
        var HashItem = new EligibilityCheckHash
        {
            EligibilityCheckHashID = Guid.NewGuid().ToString(),

            Hash = hash,
            Type = item.Type,
            Outcome = outcome,
            TimeStamp = DateTime.UtcNow,
            Source = source
        };

        await _db.EligibilityCheckHashes.AddAsync(HashItem);

        auditDataTemplate.Type = AuditType.Hash;
        auditDataTemplate.typeId = HashItem.EligibilityCheckHashID;
        await _audit.AuditAdd(auditDataTemplate);
        return HashItem.EligibilityCheckHashID;
    }
}