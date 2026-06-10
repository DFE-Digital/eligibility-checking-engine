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

    public async Task PostCheck<T>(T data, string groupId, CheckMetaData meta) where T : IEnumerable<IEligibilityServiceType>
    {
        _groupId = groupId;
        List<EligibilityCheck> mappedBulkedChecks = new(); 
        foreach (var d in data) {
           
           var item = await MapCheck(d, meta);
            mappedBulkedChecks.Add(item);
        } 

        // Insert all rows into the DB BEFORE sending any queue messages,
        // so the engine never processes a message for a row that doesn't exist yet.
        _db.BulkInsert_EligibilityCheck(mappedBulkedChecks);

        // Now send queue messages for records that weren't resolved from the hash cache.
        // Reuse a single QueueClient for all bulk messages — they share the same queue.
        var queuedBulkItems = mappedBulkedChecks.Where(x => x.Status == CheckEligibilityStatus.queuedForProcessing).ToList();
        if (queuedBulkItems.Any())
        {
            var bulkQueueName = _configuration[$"Queue:Bulk:{queuedBulkItems.First().Type}"];
            var bulkQueueClient = _storageQueueMessageGateway.GetQueueClient(bulkQueueName);
            foreach (var item in queuedBulkItems)
            {
                await _storageQueueMessageGateway.SendMessage(item, bulkQueueClient);
            }
        }
    }

    public async Task<PostCheckResult> PostCheck<T>(T data, CheckMetaData meta) where T : IEligibilityServiceType {

        var item = await MapCheck(data, meta);
        await _db.CheckEligibilities.AddAsync(item);
        await _db.SaveChangesAsync();

        // Send queue message after the row is committed to the DB.
        if (item.Status == CheckEligibilityStatus.queuedForProcessing)
        {
            var singleQueueName = _configuration[$"Queue:Single:{item.Type}"];
            var singleQueueClient = _storageQueueMessageGateway.GetQueueClient(singleQueueName);
            await _storageQueueMessageGateway.SendMessage(item, singleQueueClient);
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

    public async Task<(CheckEligibilityStatus?,EligibilityTier?)> GetStatusAsync(string guid, CheckEligibilityType type)
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid &&
                                                                           (type == CheckEligibilityType.None ||
                                                                            type == x.Type) &&
                                                                           x.IsDeleted == false);
        if (result != null) return (result.Status, result.Tier);
        return (null,null);
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
                if(record.Status == CheckEligibilityStatus.queuedForProcessing) 
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