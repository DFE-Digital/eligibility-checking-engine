// Ignore Spelling: Fsm

using AutoMapper;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CheckYourEligibility.API.Gateways;

public class CheckEligibilityGateway : BaseGateway, ICheckEligibility
{
    private const int SurnameCheckCharachters = 3;
    protected readonly IAudit _audit;
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;

    private readonly IEcsGateway _ecsGateway;
    private readonly IDwpGateway _dwpGateway;
    private readonly IHash _hashGateway;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;

    public CheckEligibilityGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        QueueServiceClient queueClientGateway,
        IConfiguration configuration, IEcsGateway ecsGateway, IDwpGateway dwpGateway, IAudit audit, IHash hashGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _mapper = mapper;
        _ecsGateway = ecsGateway;
        _dwpGateway = dwpGateway;
        _audit = audit;
        _hashGateway = hashGateway;
        _configuration = configuration;

        setQueueStandard(_configuration.GetValue<string>("QueueFsmCheckStandard"), queueClientGateway);
        setQueueBulk(_configuration.GetValue<string>("QueueFsmCheckBulk"), queueClientGateway);
    }

    public async Task<string> CreateBulkCheck(Domain.BulkCheck bulkCheck)
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

            // if (data is CheckEligibilityRequestBulkData bulkDataItem)
            // {
            //     item.ClientIdentifier = bulkDataItem.ClientIdentifier;
            // }

            item.BulkCheckID = _groupId;
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.Created = DateTime.UtcNow;
            item.Updated = DateTime.UtcNow;
            item.Status = CheckEligibilityStatus.queuedForProcessing;
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData);

            var checkHashResult =
                await _hashGateway.Exists(checkData);
            if (checkHashResult != null)
            {
                CheckEligibilityStatus hashedStatus = checkHashResult.Outcome;
                item.Status = hashedStatus;
                item.EligibilityCheckHashID = checkHashResult.EligibilityCheckHashID;
                item.EligibilityCheckHash = checkHashResult;

                // If hash is found for eligible or not eligible
                // get the first valid check and apply correct CheckData to latest check record
                // to make sure correct data is returned
                if (hashedStatus == CheckEligibilityStatus.eligible || hashedStatus == CheckEligibilityStatus.notEligible) {

                    var firstValidCheck = await _db.CheckEligibilities.Where(x => x.EligibilityCheckHashID == checkHashResult.EligibilityCheckHashID && x.Status == hashedStatus).OrderBy(x => x.Created).FirstAsync();
                    item.CheckData = firstValidCheck.CheckData;
                }               
            }

            await _db.CheckEligibilities.AddAsync(item);
            await _db.SaveChangesAsync();
            if (checkHashResult == null)
            {
                var queue = await SendMessage(item);
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

    public async Task<CheckEligibilityStatus?> ProcessCheck(string guid, AuditData auditDataTemplate)
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid &&
                                                                           x.Status != CheckEligibilityStatus.deleted);

        if (result != null)
        {
            var checkData = GetCheckProcessData(result.Type, result.CheckData);
            /*if (result.Status != CheckEligibilityStatus.queuedForProcessing)
                throw new ProcessCheckException($"Error checkItem {guid} not queuedForProcessing. {result.Status}");
*/
            switch (result.Type)
            {
                case CheckEligibilityType.FreeSchoolMeals:
                case CheckEligibilityType.TwoYearOffer:
                case CheckEligibilityType.EarlyYearPupilPremium:
                    {
                        await Process_StandardCheck(guid, auditDataTemplate, result, checkData);
                    }
                    break;
                case CheckEligibilityType.WorkingFamilies:
                    {
                        await Process_WorkingFamilies_StandardCheck(guid, auditDataTemplate, result, checkData);
                    }
                    break;
            }

            return result.Status;
        }

        return null;
    }

    public async Task<CheckEligibilityBulkDeleteResponse> DeleteByBulkCheckId(string bulkCheckId)
    {
        if (string.IsNullOrEmpty(bulkCheckId)) throw new ValidationException(null, "Invalid Request, group ID is required.");

        var response = new CheckEligibilityBulkDeleteResponse
        {
            Id = bulkCheckId,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation($"Attempting to soft delete EligibilityChecks and BulkCheck for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");
            var bulkCheckLimit = _configuration.GetValue<int>("BulkEligibilityCheckLimit");

            var records = await _db.CheckEligibilities
                .Where(x => x.BulkCheckID == bulkCheckId && x.Status != CheckEligibilityStatus.deleted)
                .ToListAsync();

            if (!records.Any())
            {
                _logger.LogWarning(
                    $"Bulk upload with ID {bulkCheckId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found or already deleted");
                throw new NotFoundException(bulkCheckId);
            }

            if (records.Count > bulkCheckLimit)
            {
                _logger.LogWarning(
                    $"Bulk upload with ID {bulkCheckId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} matched {records.Count} records, exceeding {bulkCheckLimit} max — operation aborted.");
                response.Success = false;
                response.DeletedCount = 0;
                response.Error = $"Too many records ({records.Count}) matched. Max allowed per bulk group is {bulkCheckLimit}.";
                return response;
            }

            // Soft delete the EligibilityCheck records by setting status to deleted
            foreach (var record in records)
            {
                record.Status = CheckEligibilityStatus.deleted;
                record.Updated = DateTime.UtcNow;
            }

            // Also soft delete the corresponding BulkCheck record
            var bulkCheck = await _db.BulkChecks.FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId && x.Status != BulkCheckStatus.Deleted);
            if (bulkCheck != null)
            {
                bulkCheck.Status = BulkCheckStatus.Deleted;
                _logger.LogInformation($"Found and marked BulkCheck record for soft deletion: {bulkCheckId?.Replace(Environment.NewLine, "")}");
            }
            else
            {
                _logger.LogWarning($"BulkCheck record not found or already deleted for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation($"Soft deleted {records.Count} EligibilityChecks and associated BulkCheck for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");

            response.Success = true;
            response.DeletedCount = records.Count;
            response.Message = $"{records.Count} eligibility check record(s) and associated bulk check successfully deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting EligibilityChecks for Group: {bulkCheckId?.Replace(Environment.NewLine, "")}");

            response.Success = false;
            response.DeletedCount = 0;
            response.Error = $"Error during deletion: {ex.Message}";
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
                    var item = await GetItem<CheckEligibilityItem>(result.EligibilityCheckID, result.Type,
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

    public async Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid,
        EligibilityCheckStatusData data)
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid && x.Status != CheckEligibilityStatus.deleted);
        if (result != null)
        {
            result.Status = data.Status;
            result.Updated = DateTime.UtcNow;
            var updates = await _db.SaveChangesAsync();
            return new CheckEligibilityStatusResponse { Data = new StatusValue { Status = result.Status.ToString() } };
        }

        return null;
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
    public async Task<IEnumerable<Domain.BulkCheck>?> GetBulkStatuses(string localAuthorityId, IList<int> allowedLocalAuthorityIds, bool includeLast7DaysOnly = true)
    {
        // Parse the requested local authority ID
        if (!int.TryParse(localAuthorityId, out var requestedLAId))
        {
            return new List<Domain.BulkCheck>();
        }

        // Build optimized query with compound filtering
        IQueryable<Domain.BulkCheck> query = _db.BulkChecks
            .Where(x => x.Status != BulkCheckStatus.Deleted); // Exclude deleted records

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
                query = query.Where(x => x.LocalAuthorityId == requestedLAId);
            }
        }
        else
        {
            // Regular user - check if they have permission for the requested LA
            if (allowedLocalAuthorityIds.Contains(requestedLAId))
            {
                // User has access to the specific requested local authority
                query = query.Where(x => x.LocalAuthorityId == requestedLAId);
            }
            else
            {
                // User doesn't have access to the requested local authority - early return
                return new List<Domain.BulkCheck>();
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

    public async Task<Domain.BulkCheck?> GetBulkCheck(string guid)
    {
        var bulkCheck = await _db.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == guid && x.Status != BulkCheckStatus.Deleted);

        return bulkCheck;
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

    private CheckEligibilityStatus TestDataCheck(string? nino, string? nass)
    {
        if (!nino.IsNullOrEmpty())
        {
            if (nino.StartsWith(_configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Eligible")))
                return CheckEligibilityStatus.eligible;
            if (nino.StartsWith(
                    _configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:NotEligible")))
                return CheckEligibilityStatus.notEligible;
            if (nino.StartsWith(
                    _configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:ParentNotFound")))
                return CheckEligibilityStatus.parentNotFound;
            if (nino.StartsWith(_configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Error")))
                return CheckEligibilityStatus.error;
        }
        else
        {
            nass = nass.Substring(2, 2);
            if (nass == _configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Eligible"))
                return CheckEligibilityStatus.eligible;
            if (nass == _configuration.GetValue<string>(
                    "TestData:Outcomes:NationalAsylumSeekerServiceNumber:NotEligible"))
                return CheckEligibilityStatus.notEligible;
            if (nass == _configuration.GetValue<string>(
                    "TestData:Outcomes:NationalAsylumSeekerServiceNumber:ParentNotFound"))
                return CheckEligibilityStatus.parentNotFound;
            if (nass == _configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Error"))
                return CheckEligibilityStatus.error;
        }

        return CheckEligibilityStatus.parentNotFound;
    }

    #region Private

    [ExcludeFromCodeCoverage]
    private void setQueueStandard(string queName, QueueServiceClient queueClientGateway)
    {
        if (queName != "notSet") _queueClientStandard = queueClientGateway.GetQueueClient(queName);
    }

    [ExcludeFromCodeCoverage]
    private void setQueueBulk(string queName, QueueServiceClient queueClientGateway)
    {
        if (queName != "notSet") _queueClientBulk = queueClientGateway.GetQueueClient(queName);
    }

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    private async Task<string> SendMessage(EligibilityCheck item)
    {
        var queueName = string.Empty;
        if (_queueClientStandard != null)
        {
            if (item.BulkCheckID.IsNullOrEmpty())
            {
                await _queueClientStandard.SendMessageAsync(
                    JsonConvert.SerializeObject(new QueueMessageCheck
                    {
                        Type = item.Type.ToString(),
                        Guid = item.EligibilityCheckID,
                        ProcessUrl = $"{CheckLinks.ProcessLink}{item.EligibilityCheckID}",
                        SetStatusUrl = $"{CheckLinks.GetLink}{item.EligibilityCheckID}/status"
                    }));

                LogQueueCount(_queueClientStandard);
                queueName = _queueClientStandard.Name;
            }
            else
            {
                await _queueClientBulk.SendMessageAsync(
                    JsonConvert.SerializeObject(new QueueMessageCheck
                    {
                        Type = item.Type.ToString(),
                        Guid = item.EligibilityCheckID,
                        ProcessUrl = $"{CheckLinks.ProcessLink}{item.EligibilityCheckID}",
                        SetStatusUrl = $"{CheckLinks.GetLink}{item.EligibilityCheckID}/status"
                    }));
                LogQueueCount(_queueClientBulk);
                queueName = _queueClientBulk.Name;
            }
        }

        return queueName;
    }

    /// <summary>
    /// Logic to find a match in Working families events' table
    /// Checks if record with the same EligibilityCode-ParentNINO-ChildDOB-ParentLastName exists in the WorkingFamiliesEvents Table
    /// </summary>
    /// <param name="checkData"></param>
    /// <returns></returns>
    private async Task<WorkingFamiliesEvent> Check_Working_Families_EventRecord(string dateOfBirth,
        string eligibilityCode, string nino, string lastName)
    {
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
        DateTime checkDob = DateTime.ParseExact(dateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var wfRecords = await _db.WorkingFamiliesEvents.Where(x =>
            x.EligibilityCode == eligibilityCode &&
            (x.ParentNationalInsuranceNumber == nino || x.PartnerNationalInsuranceNumber == nino) &&
            (lastName == null || lastName == "" || x.ParentLastName.ToUpper() == lastName ||
             x.PartnerLastName.ToUpper() == lastName) &&
            x.ChildDateOfBirth == checkDob).OrderByDescending(x => x.SubmissionDate).Take(2).ToListAsync();

        // If there is more than one record
        // check if second to last record has not expired yet
        // set the event to the second record that is still valid
        // and get set ValidityEndDate and the GracePeriodEndDate of the future record
        if (wfRecords.Count() > 1 && wfRecords[1].ValidityEndDate > DateTime.UtcNow)
        {
            wfEvent = wfRecords[1];

            wfEvent.ValidityEndDate = wfRecords[0].ValidityEndDate;
            wfEvent.GracePeriodEndDate = wfRecords[0].GracePeriodEndDate;
        }
        else
        {
            wfEvent = wfRecords.FirstOrDefault();
        }

        return wfEvent;
    }
    /// <summary>
    /// This method is used for generating test data in runtime
    /// If code starts with 900 it will generate an event record that must return Eligible
    /// If code starts with 901 it will generate an event record that must return notEligible
    /// If code starts with 902 it will generate an event record that must return NotFound
    /// If code starts with 903 it will generate an event record that must return Error
    /// </summary>
    /// <param name="checkData"></param>
    /// <returns></returns>
    private async Task<WorkingFamiliesEvent> Generate_Test_Working_Families_EventRecord(CheckProcessData checkData)
    {
        string eligibilityCode = checkData.EligibilityCode;
        var prefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible");
        bool isEligiblePrefix = !prefix.IsNullOrEmpty() && eligibilityCode.StartsWith(prefix);
        DateTime today = DateTime.UtcNow;
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
        if (isEligiblePrefix)
        {
            wfEvent.ValidityStartDate = today.AddDays(-1);
            wfEvent.DiscretionaryValidityStartDate = today.AddDays(-1);
            wfEvent.ValidityEndDate = today.AddMonths(3);
            wfEvent.GracePeriodEndDate = today.AddMonths(6);
            wfEvent.SubmissionDate = new DateTime(today.Year, today.AddMonths(-1).Month, 25);
            wfEvent.ParentLastName = checkData.LastName ?? "TESTER";
            wfEvent.EligibilityCode = eligibilityCode;
        }
        else
        {
            wfEvent = null;
        }
        return wfEvent;
    }
    /// <summary>
    /// Checks if record with the same EligibilityCode-ParentNINO-ChildDOB-ParentLastName exists in the WorkingFamiliesEvents Table
    /// If record is found, process logic to determine eligibility
    /// Code is considered 'eligible' if the current date is between the DiscretionaryValidityStartDate and ValidityEndDate or 
    /// between the DiscretionaryValidityStartDate and the GracePeriodEndDate.
    /// Else change status to 'notEligible'
    /// If record is not found in WorkingFamiliesEvents table - change status to 'notFound'
    /// </summary>
    /// <returns></returns>
    private async Task Process_WorkingFamilies_StandardCheck(string guid, AuditData auditDataTemplate,
        EligibilityCheck? result, CheckProcessData checkData)
    {
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
        var source = ProcessEligibilityCheckSource.HMRC;
        string wfTestCodePrefix = _configuration.GetValue<string>("TestData:WFTestCodePrefix");

        result.Status = CheckEligibilityStatus.notFound;

        // Get event for test record
        if (!string.IsNullOrEmpty(wfTestCodePrefix) &&
            checkData.EligibilityCode.StartsWith(wfTestCodePrefix))
        {
            wfEvent = await Generate_Test_Working_Families_EventRecord(checkData);
        }

        // Get event for ecs record
        else if (_ecsGateway.UseEcsforChecksWF == "true")
        {
            //To ensure correct LA ID is passed when using ECS for checks
            string laId = ExtractLAIdFromScope(auditDataTemplate.scope);
            SoapCheckResponse innerResult = await _ecsGateway.EcsWFCheck(checkData, laId);

            result.Status = convertEcsResultStatus(innerResult);
            if (result.Status != CheckEligibilityStatus.notFound && result.Status != CheckEligibilityStatus.error)
            {
                wfEvent.EligibilityCode = checkData.EligibilityCode;
                wfEvent.ParentLastName = innerResult.ParentSurname;
                wfEvent.DiscretionaryValidityStartDate = DateTime.Parse(innerResult.ValidityStartDate);
                wfEvent.ValidityEndDate = DateTime.Parse(innerResult.ValidityEndDate);
                wfEvent.GracePeriodEndDate = DateTime.Parse(innerResult.GracePeriodEndDate);
            }

            source = ProcessEligibilityCheckSource.ECS;
        }

        // Get event for ECE record
        else
        {
            wfEvent = await Check_Working_Families_EventRecord(checkData.DateOfBirth, checkData.EligibilityCode,
          checkData.NationalInsuranceNumber, checkData.LastName);
        }

        var wfCheckData = JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);
        
        // If event is returned inititiate business logic. 
        if (result.Status != CheckEligibilityStatus.notFound || (wfEvent != null && wfEvent.EligibilityCode != null))
        {
            //Get current date and ensure it is between the DiscretionaryValidityStartDate and GracePeriodEndDate
            var currentDate = DateTime.UtcNow.Date;

            if (currentDate >= wfEvent.DiscretionaryValidityStartDate && currentDate <= wfEvent.GracePeriodEndDate)
            {
                result.Status = CheckEligibilityStatus.eligible;
            }
            else
            {
                result.Status = CheckEligibilityStatus.notEligible;
            }
        }
        // Create hash just with the check request data to match on post requests
        result.EligibilityCheckHashID =
           await _hashGateway.Create(wfCheckData, result.Status, source, auditDataTemplate);

        // Now update the check data in the EligibilityCheckTable with all the neccessary fields
        // that needs to be returned on the GET request.
        if (wfEvent != null)
        {
            wfCheckData.ValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
            wfCheckData.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
            wfCheckData.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
            wfCheckData.LastName = wfEvent.ParentLastName;
            wfCheckData.SubmissionDate = wfEvent.SubmissionDate.ToString("yyyy-MM-dd");

            result.CheckData = JsonConvert.SerializeObject(wfCheckData);
            _db.CheckEligibilities.Update(result);
        }

        result.Updated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
    /// <summary>
    /// Extract LA Id from scope if it exists
    /// else return an empty string.
    /// </summary>
    /// <param name="scope"></param>
    /// <returns></returns>
    private string ExtractLAIdFromScope(string scope)
    {
        string laWithIdSyntax = "local_authority:";
        string laId = string.Empty;

        if (!string.IsNullOrEmpty(scope) && scope.Contains(laWithIdSyntax))
        {
            int LaIdStartIndex = scope.IndexOf(laWithIdSyntax) + laWithIdSyntax.Length;
            var LaIdendIndex = scope.IndexOf(" ", LaIdStartIndex);
            if (LaIdendIndex == -1)
            {
                laId = scope.Substring(LaIdStartIndex).Trim();
            }
            else
            {
                laId = scope.Substring(LaIdStartIndex, LaIdendIndex - LaIdStartIndex).Trim();
            }

        }
        return laId;
    }
    private async Task Process_StandardCheck(string guid, AuditData auditDataTemplate, EligibilityCheck? result,
        CheckProcessData checkData)
    {
        var source = ProcessEligibilityCheckSource.HMRC;
        var checkResult = CheckEligibilityStatus.parentNotFound;
        CAPIClaimResponse capiClaimResponse = new();
        // Variables needed for ECS conflict records
        var eceCheckResult = CheckEligibilityStatus.parentNotFound;
        string correlationId = Guid.NewGuid().ToString(); // for CAPI request to track request from DWP side
        if (_configuration.GetValue<string>("TestData:LastName") == checkData.LastName)
        {
            checkResult = TestDataCheck(checkData.NationalInsuranceNumber, checkData.NationalAsylumSeekerServiceNumber);
            source = ProcessEligibilityCheckSource.TEST;
        }

        else
        {
            if (!checkData.NationalInsuranceNumber.IsNullOrEmpty())
            {
                //To ensure correct LA ID is passed when using ECS for checks
                string laId = ExtractLAIdFromScope(auditDataTemplate.scope);
                checkResult = await HMRC_Check(checkData);
                if (checkResult == CheckEligibilityStatus.parentNotFound)
                {
                    if (_ecsGateway.UseEcsforChecks == "true")
                    {

                        checkResult = await EcsCheck(checkData, laId);
                        source = ProcessEligibilityCheckSource.ECS;

                    }
                    else if (_ecsGateway.UseEcsforChecks == "false")
                    {

                        capiClaimResponse = await DwpCitizenCheck(checkData, checkResult, correlationId);
                        checkResult = capiClaimResponse.CheckEligibilityStatus;
                        source = ProcessEligibilityCheckSource.DWP;
                    }
                    else // do both checks
                    {
                        checkResult = await EcsCheck(checkData, laId);
                        source = ProcessEligibilityCheckSource.DWP;
                        capiClaimResponse = await DwpCitizenCheck(checkData, checkResult, correlationId);
                        eceCheckResult = capiClaimResponse.CheckEligibilityStatus;

                        if (checkResult != eceCheckResult)
                        {
                            source = ProcessEligibilityCheckSource.ECS_CONFLICT;
                        }

                    }

                }
            }
            else if (!checkData.NationalAsylumSeekerServiceNumber.IsNullOrEmpty())
            {
                checkResult = await HO_Check(checkData);
                source = ProcessEligibilityCheckSource.HO;
            }
        }

        result.Status = checkResult;
        result.Updated = DateTime.UtcNow;

        if (checkResult == CheckEligibilityStatus.error)
        {
            // Revert status back and do not save changes
            result.Status = CheckEligibilityStatus.queuedForProcessing;
            TrackMetric("Dwp Error", 1);
        }
        else
        {
            result.EligibilityCheckHashID =
              await _hashGateway.Create(checkData, checkResult, source, auditDataTemplate);

            //If CAPI returns a different result from ECS
            // Create a record
            if (source == ProcessEligibilityCheckSource.ECS_CONFLICT)
            {
                var organisation = await _db.Audits.FirstOrDefaultAsync(a => a.typeId == guid);
                ECSConflict ecsConflictRecord = new()
                {

                    CorrelationId = correlationId,
                    ECE_Status = eceCheckResult,
                    ECS_Status = checkResult,
                    DateOfBirth = checkData.DateOfBirth,
                    LastName = checkData.LastName,
                    Nino = checkData.NationalInsuranceNumber,
                    Type = checkData.Type,
                    Organisation = organisation.authentication,
                    TimeStamp = DateTime.UtcNow,
                    EligibilityCheckHashID = result.EligibilityCheckHashID,
                    CAPIEndpoint = capiClaimResponse.CAPIEndpoint,
                    Reason = capiClaimResponse.Reason,
                    CAPIResponseCode = capiClaimResponse.CAPIResponseCode


                };
                await _db.ECSConflicts.AddAsync(ecsConflictRecord);

            }

            await _db.SaveChangesAsync();
        }

        TrackMetric($"FSM Check:-{result.Status}", 1);
        TrackMetric("FSM Check", 1);
        var processingTime = (DateTime.Now.ToUniversalTime() - result.Created.ToUniversalTime()).Seconds;
        TrackMetric("Check ProcessingTime (Seconds)", processingTime);
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityType type, string data)
    {
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

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    private void LogQueueCount(QueueClient queue)
    {
        QueueProperties properties = queue.GetProperties();

        // Retrieve the cached approximate message count
        var cachedMessagesCount = properties.ApproximateMessagesCount;
        TrackMetric($"QueueCount:-{_queueClientStandard.Name}", cachedMessagesCount);
    }

    private async Task<CheckEligibilityStatus> HO_Check(CheckProcessData data)
    {
        var checkReults = _db.FreeSchoolMealsHO.Where(x =>
                x.NASS == data.NationalAsylumSeekerServiceNumber
                && x.DateOfBirth == DateTime.ParseExact(data.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None))
            .Select(x => x.LastName);
        return CheckSurname(data.LastName, checkReults);
    }

    private async Task<CheckEligibilityStatus> HMRC_Check(CheckProcessData data)
    {
        var checkReults = _db.FreeSchoolMealsHMRC.Where(x =>
                x.FreeSchoolMealsHMRCID == data.NationalInsuranceNumber
                && x.DateOfBirth == DateTime.ParseExact(data.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None))
            .Select(x => x.Surname);

        return CheckSurname(data.LastName, checkReults);
    }

    private CheckEligibilityStatus convertEcsResultStatus(SoapCheckResponse? result)
    {
        if (result != null)
        {
            if (result.Status == "1")
            {
                return CheckEligibilityStatus.eligible;
            }
            else if (result.Status == "0" && result.ErrorCode == "0" && result.Qualifier.IsNullOrEmpty())
            {
                return CheckEligibilityStatus.notEligible;
            }
            else if (result.Status == "0" && result.ErrorCode == "0" && result.Qualifier == "No Trace - Check data")
            {
                return CheckEligibilityStatus.parentNotFound;
            }
            else
            {
                _logger.LogError(
                    $"Error unknown Response status code:-{result.Status}, error code:-{result.ErrorCode} qualifier:-{result.Qualifier}");
                return CheckEligibilityStatus.error;
            }
        }
        else
        {
            _logger.LogError("Error ECS unknown Response of null");
            return CheckEligibilityStatus.error;
        }
    }

    private async Task<CheckEligibilityStatus> EcsCheck(CheckProcessData data, string LaId)
    {
        //check for benefit
        var result = await _ecsGateway.EcsCheck(data, data.Type, LaId);
        return convertEcsResultStatus(result);
    }

    public async Task<CAPIClaimResponse> DwpCitizenCheck(CheckProcessData data,
        CheckEligibilityStatus checkResult, string correlationId)
    {

        // ECS_Conflict helper logic to better track conflicts
        CAPIClaimResponse claimResponse = new();

        var citizenRequest = new CitizenMatchRequest
        {
            Jsonapi = new CitizenMatchRequest.CitizenMatchRequest_Jsonapi { Version = "1.0" },
            Data = new CitizenMatchRequest.CitizenMatchRequest_Data
            {
                Type = "Match",
                Attributes = new CitizenMatchRequest.CitizenMatchRequest_Attributes
                {
                    LastName = data.LastName,
                    NinoFragment = data.NationalInsuranceNumber.Substring(data.NationalInsuranceNumber.Length - 5, 4),
                    DateOfBirth = data.DateOfBirth
                }
            }
        };
        //check citizen
        // if a guid empty ie the request failed then the status is updated

        _logger.LogInformation($"Dwp before getting citizen");

        _logger.LogInformation(JsonConvert.SerializeObject(citizenRequest));
        var citizenResponse = await _dwpGateway.GetCitizen(citizenRequest, data.Type, correlationId);
        _logger.LogInformation($"Dwp after getting citizen");

        if (string.IsNullOrEmpty(citizenResponse.Guid))
        {
            _logger.LogInformation($"Dwp after getting citizen error " + citizenResponse.CheckEligibilityStatus);
            return citizenResponse;
        }
        // Guid returned = citizen found
        else
        {
            _logger.LogInformation($"Dwp has valid citizen");

            // Perform a benefit check
            var result = await _dwpGateway.GetCitizenClaims(citizenResponse.Guid, DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
            DateTime.Now.ToString("yyyy-MM-dd"), data.Type, correlationId);
            _logger.LogInformation($"Dwp after getting claim");

            if (result.Item1.StatusCode == StatusCodes.Status200OK)
            {
                checkResult = CheckEligibilityStatus.eligible;
                _logger.LogInformation($"Dwp is eligible");

            }
            else if (result.Item1.StatusCode == StatusCodes.Status404NotFound)
            {
                checkResult = CheckEligibilityStatus.notEligible;

                _logger.LogInformation($"Dwp is not found");
            }
            else
            {
                _logger.LogError($"Dwp Error unknown Response status code:-{result.Item1.StatusCode}.");
                checkResult = CheckEligibilityStatus.error;
            }
            // ECS_Conflict helper logic to better track conflicts
            claimResponse.CAPIEndpoint =
               $"v2/citizens/{citizenResponse.Guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based";
            claimResponse.CheckEligibilityStatus = checkResult;
            claimResponse.Reason = result.Item2; // reason message returned from DWP gateway
            claimResponse.CAPIResponseCode = (HttpStatusCode)result.Item1.StatusCode;
        }
        return claimResponse;
    }

    private CheckEligibilityStatus CheckSurname(string lastNamePartial, IQueryable<string> validData)
    {
        if (validData.Any())
            return validData.FirstOrDefault(x =>
                x.ToUpper().StartsWith(lastNamePartial.Substring(0, SurnameCheckCharachters).ToUpper())) != null
                ? CheckEligibilityStatus.eligible
                : CheckEligibilityStatus.parentNotFound;
        ;
        return CheckEligibilityStatus.parentNotFound;
    }

    public async Task ProcessQueue(string queName)
    {
        QueueClient queue;
        if (queName == _configuration.GetValue<string>("QueueFsmCheckStandard"))
            queue = _queueClientStandard;
        else if (queName == _configuration.GetValue<string>("QueueFsmCheckBulk"))
            queue = _queueClientBulk;
        else
            throw new Exception($"invalid queue {queName}.");
        if (await queue.ExistsAsync())
        {
            QueueProperties properties = await queue.GetPropertiesAsync();

            if (properties.ApproximateMessagesCount > 0)
            {
                QueueMessage[] retrievedMessage = await queue.ReceiveMessagesAsync(32);
                foreach (var item in retrievedMessage)
                {
                    var checkData =
                        JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(item.Body));
                    try
                    {
                        var postCheckAudit = await _db.Audits.FirstOrDefaultAsync(a => a.typeId == checkData.Guid && a.Type == AuditType.Check && a.method == "POST");
                        string scope = string.Empty;
                        if (postCheckAudit != null && postCheckAudit.scope != null) scope = postCheckAudit.scope;

                        var result = await ProcessCheck(checkData.Guid, new AuditData
                        {
                            Type = AuditType.Check,
                            typeId = checkData.Guid,
                            authentication = queName,
                            method = "processQue",
                            source = "queueProcess",
                            url = ".",
                            scope = scope
                        });
                        // When status is Queued For Processing, i.e. not error
                        if (result == CheckEligibilityStatus.queuedForProcessing)
                        {
                            //If item doesn't exist, or we've tried more than retry limit
                            if (result == null || item.DequeueCount >= _configuration.GetValue<int>("QueueRetries"))
                            {
                                //Delete message and update status to error
                                await UpdateEligibilityCheckStatus(checkData.Guid,
                                    new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error });
                                await queue.DeleteMessageAsync(item.MessageId, item.PopReceipt);
                            }
                        }
                        // If status is not queued for Processing, we have a conclusive answer
                        else
                        {
                            await queue.DeleteMessageAsync(item.MessageId, item.PopReceipt);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Queue processing");
                        // If we've had exceptions on this item more than retry limit
                        if (item.DequeueCount >= _configuration.GetValue<int>("QueueRetries"))
                            await queue.DeleteMessageAsync(item.MessageId, item.PopReceipt);
                    }
                }

                properties = await queue.GetPropertiesAsync();
            }
        }
    }

    #endregion
}