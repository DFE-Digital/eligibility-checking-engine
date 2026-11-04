using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Gateways;

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

    public async Task<CheckProcessData?> GetCachedCheckDataAsync(
        CheckProcessData checkData,
        EligibilityCheckHash hashResult)
    {
        var type = checkData.Type;
        if ((type != CheckEligibilityType.WorkingFamilies && type != CheckEligibilityType.FreeSchoolMeals) ||
            (hashResult.Outcome != CheckEligibilityStatus.eligible && hashResult.Outcome != CheckEligibilityStatus.notEligible))
        {
            return null;
        }

        try
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var matchingChecks = _db.CheckEligibilities
                        .Where(x => x.EligibilityCheckHashID == hashResult.EligibilityCheckHashID && x.Status == hashResult.Outcome)
                        .AsNoTracking();
                    var orderedChecks = type == CheckEligibilityType.WorkingFamilies
                        ? matchingChecks.OrderBy(x => x.Created)
                        : matchingChecks.OrderByDescending(x => x.Created);
                    var firstValidCheck = await orderedChecks.FirstOrDefaultAsync();

                    if (firstValidCheck != null)
                    {
                        var cachedCheckData = JsonConvert.DeserializeObject<CheckProcessData>(firstValidCheck.CheckData);
                        var mergedCheckData = type == CheckEligibilityType.WorkingFamilies
                            ? MergeWorkingFamiliesCheckData(cachedCheckData, checkData)
                            : MergeFreeSchoolMealsCheckData(cachedCheckData, checkData);
                        _logger.LogInformation($"Action: Retrieve check with HashID:{hashResult.EligibilityCheckHashID}, Status:Found, Attempt:{attempt}");
                        return mergedCheckData;
                    }

                    _logger.LogWarning($"Action: Retrieve check with HashID:{hashResult.EligibilityCheckHashID}, Status:NotFound, Attempt:{attempt}");
                }
                catch (SqlException ex) when (ex.Number == -2) // SQL timeout
                {
                    _logger.LogWarning($"Action: Retrieve check with HashID:{hashResult.EligibilityCheckHashID}, Status:SQLTimeout, Attempt:{attempt}");
                }

                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating check with ID: {hashResult.EligibilityCheckHashID}");
            throw ex;
        }
        return null;
    }

    private static CheckProcessData MergeWorkingFamiliesCheckData(
        CheckProcessData cachedCheckData,
        CheckProcessData checkData)
    {
        cachedCheckData.ClientIdentifier = checkData.ClientIdentifier;
        cachedCheckData.Order = checkData.Order;
        cachedCheckData.Reference = checkData.Reference;
        return cachedCheckData;
    }

    private static CheckProcessData MergeFreeSchoolMealsCheckData(
        CheckProcessData cachedCheckData,
        CheckProcessData checkData)
    {
        cachedCheckData.ClientIdentifier = checkData.ClientIdentifier;
        cachedCheckData.Order = checkData.Order;
        cachedCheckData.Reference = checkData.Reference;
        cachedCheckData.FirstName = checkData.FirstName;
        cachedCheckData.ChildFirstName = checkData.ChildFirstName;
        cachedCheckData.ChildLastName = checkData.ChildLastName;
        cachedCheckData.ChildDateOfBirth = checkData.ChildDateOfBirth;
        cachedCheckData.ChildSchoolURN = checkData.ChildSchoolURN;
        cachedCheckData.EmailAddress = checkData.EmailAddress;
        return cachedCheckData;
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
        ProcessEligibilityCheckSource source, EligibilityCheckContext dbContextFactory = null)
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