// Ignore Spelling: Fsm

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
    private readonly IStorageQueueMessage _storageQueueMessageGateway;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;
    private string _groupId;

    public CheckEligibilityGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        IConfiguration configuration, IHash hashGateway, IStorageQueueMessage storageQueueMessageGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _mapper = mapper;
        _hashGateway = hashGateway;
        _storageQueueMessageGateway = storageQueueMessageGateway;
        _configuration = configuration;
    }

    public async Task PostCheck<T>(T data, string groupId) where T : IEnumerable<IEligibilityServiceType>
    {
        _groupId = groupId;
        foreach (var item in data) await PostCheck(item);
    }

    public async Task<PostCheckResult> PostCheck<T>(T data) where T : IEligibilityServiceType
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
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData);

            //TODO: The hashing logic should sit in the use case, targeting the hash gateway
            var checkHashResult =
                await _hashGateway.Exists(checkData);
            if (checkHashResult != null)
            {
                CheckEligibilityStatus hashedStatus = checkHashResult.Outcome;
                item.Status = hashedStatus;
                item.EligibilityCheckHashID = checkHashResult.EligibilityCheckHashID;
                item.EligibilityCheckHash = checkHashResult;

                if (data.Type==CheckEligibilityType.WorkingFamilies&&(hashedStatus == CheckEligibilityStatus.eligible || hashedStatus == CheckEligibilityStatus.notEligible)) {
                    try
                    {
                        var firstValidCheck = await _db.CheckEligibilities
                            .Where(x => x.EligibilityCheckHashID == checkHashResult.EligibilityCheckHashID &&
                                        x.Status == hashedStatus).OrderByDescending(x => x.Created).FirstAsync();

                        CheckProcessData hashCheckData = JsonConvert.DeserializeObject<CheckProcessData>(firstValidCheck.CheckData);
                        hashCheckData.ClientIdentifier = checkData.ClientIdentifier;
                        
                        item.CheckData = JsonConvert.SerializeObject(hashCheckData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating check with ID: {GroupId}", _groupId);
                    }
                }
            }

            await _db.CheckEligibilities.AddAsync(item);
            await _db.SaveChangesAsync();
            //TODO: The message queueing logic should sit in the use case, targeting the storage queue gateway
            if (checkHashResult == null)
            {
                var queue = await _storageQueueMessageGateway.SendMessage(item);
            }

            return new PostCheckResult { Id = item.EligibilityCheckID, Status = item.Status };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Db post");
            throw;
        }
    }

    public async Task<CheckEligibilityStatus?> GetStatus(string guid, CheckEligibilityType type)
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid &&
                                                                           (type == CheckEligibilityType.None ||
                                                                            type == x.Type) &&
                                                                           x.Status != CheckEligibilityStatus.deleted);
        if (result != null) return result.Status;
        return null;
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

            // Soft delete the EligibilityCheck records by setting status to deleted
            foreach (var record in records)
            {
                record.Status = CheckEligibilityStatus.deleted;
                record.Updated = DateTime.UtcNow;
            }

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
                                                                           x.Status != CheckEligibilityStatus.deleted);


        var item = _mapper.Map<CheckEligibilityItem>(result);
        if (result != null)
        {
            var CheckData = GetCheckProcessData(result.Type, result.CheckData);
            if (isBatchRecord)
            {
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
                    break;
            }

            return (T)item;
        }

        return default;
    }

    public async Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid,
        EligibilityCheckStatusData data, EligibilityCheckContext? dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var result = await context.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid && x.Status != CheckEligibilityStatus.deleted);
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
            ? item.NationalAsylumSeekerServiceNumber.ToUpper()
            : item.NationalInsuranceNumber.ToUpper();
        var input = $"{item.LastName.ToUpper()}{key}{item.DateOfBirth}{item.Type}";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var inputHash = SHA256.HashData(inputBytes);
        return Convert.ToHexString(inputHash);
    }

    #region Private
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
                    NationalAsylumSeekerServiceNumber = checkItem.NationalAsylumSeekerServiceNumber,
                    NationalInsuranceNumber = checkItem.NationalInsuranceNumber,
                    Type = type,
                    ClientIdentifier = checkItem.ClientIdentifier
                };
        }
    }

    #endregion
}