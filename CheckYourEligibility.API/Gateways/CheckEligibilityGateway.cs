// Ignore Spelling: Fsm

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
using NetTopologySuite.Index.HPRtree;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
            return bulkCheck.Guid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk check with ID: {GroupId}", bulkCheck.Guid);
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

            item.Group = _groupId;
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.Created = DateTime.UtcNow;
            item.Updated = DateTime.UtcNow;

            item.Status = CheckEligibilityStatus.queuedForProcessing;
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData);

            var checkHashResult =
                await _hashGateway.Exists(checkData);
            if (checkHashResult != null)
            {
                item.Status = checkHashResult.Outcome;
                item.EligibilityCheckHashID = checkHashResult.EligibilityCheckHashID;
                item.EligibilityCheckHash = checkHashResult;
            }

            await _db.CheckEligibilities.AddAsync(item);
            await _db.SaveChangesAsync();
            if (checkHashResult == null)
            {
                var queue = await SendMessage(item);
            }
            else
            {
                await UpdateBulkCheckStatusIfComplete(item.EligibilityCheckID);
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

    public async Task<CheckEligibilityBulkDeleteResponse> DeleteByGroup(string groupId)
    {
        if (string.IsNullOrEmpty(groupId)) throw new ValidationException(null, "Invalid Request, group ID is required.");

        var response = new CheckEligibilityBulkDeleteResponse
        {
            GroupId = groupId,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation($"Attempting to soft delete EligibilityChecks and BulkCheck for Group: {groupId?.Replace(Environment.NewLine, "")}");

            var records = await _db.CheckEligibilities
                .Where(x => x.Group == groupId && x.Status != CheckEligibilityStatus.deleted)
                .ToListAsync();

            if (!records.Any())
            {
                _logger.LogWarning(
                    $"Bulk upload with ID {groupId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found or already deleted");
                throw new NotFoundException(groupId);
            }
            
            if (records.Count > 250)
            {
                _logger.LogWarning(
                    $"Bulk upload with ID {groupId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} matched {records.Count} records, exceeding 250 max — operation aborted.");
                response.Success = false;
                response.DeletedCount = 0;
                response.Error = $"Too many records ({records.Count}) matched. Max allowed per bulk group is 250.";
                return response;
            }

            // Soft delete the EligibilityCheck records by setting status to deleted
            foreach (var record in records)
            {
                record.Status = CheckEligibilityStatus.deleted;
                record.Updated = DateTime.UtcNow;
            }
            
            // Also soft delete the corresponding BulkCheck record
            var bulkCheck = await _db.BulkChecks.FirstOrDefaultAsync(x => x.Guid == groupId && x.Status != BulkCheckStatus.Deleted);
            if (bulkCheck != null)
            {
                bulkCheck.Status = BulkCheckStatus.Deleted;
                _logger.LogInformation($"Found and marked BulkCheck record for soft deletion: {groupId?.Replace(Environment.NewLine, "")}");
            }
            else
            {
                _logger.LogWarning($"BulkCheck record not found or already deleted for Group: {groupId?.Replace(Environment.NewLine, "")}");
            }
            
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Soft deleted {records.Count} EligibilityChecks and associated BulkCheck for Group: {groupId?.Replace(Environment.NewLine, "")}");

            response.Success = true;
            response.DeletedCount = records.Count;
            response.Message = $"{records.Count} eligibility check record(s) and associated bulk check successfully deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting EligibilityChecks for Group: {groupId?.Replace(Environment.NewLine, "")}");

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
            .Where(x => x.Group == guid && x.Status != CheckEligibilityStatus.deleted).ToList();
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
            .Where(x => x.Group == guid && x.Status != CheckEligibilityStatus.deleted)
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
                    expectedStatus = bulkCheck.Status == BulkCheckStatus.Deleted ? BulkCheckStatus.Deleted : bulkCheck.Status;
                }
                else if (queuedCount == totalCount)
                {
                    expectedStatus = BulkCheckStatus.InProgress; // All queued
                }
                else if (queuedCount == 0)
                {
                    expectedStatus = errorCount > 0 ? BulkCheckStatus.Failed : BulkCheckStatus.Completed; // All completed
                }
                else
                {
                    expectedStatus = BulkCheckStatus.InProgress; // Partially completed
                }

                // Update status if needed (but don't save to avoid side effects during read)
                if (bulkCheck.Status != expectedStatus && bulkCheck.Status != BulkCheckStatus.Deleted)
                {
                    bulkCheck.Status = expectedStatus;
                    // Note: Status will be persisted during the next ProcessQueue cycle
                }
            }
        }

        return bulkChecks;
    }

    public async Task<Domain.BulkCheck?> GetBulkCheck(string guid)
    {
        var bulkCheck = await _db.BulkChecks
            .FirstOrDefaultAsync(x => x.Guid == guid && x.Status != BulkCheckStatus.Deleted);
        
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
            if (item.Group.IsNullOrEmpty())
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
        if (wfRecords.Count() > 1 && wfRecords[1].ValidityEndDate > DateTime.UtcNow)
        {           
                wfEvent = wfRecords[1];  
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
    private async Task<WorkingFamiliesEvent> Generate_Test_Working_Families_EventRecord(CheckProcessData checkData) { 
        string eligibilityCode = checkData.EligibilityCode;
        var prefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible");
        bool isEligiblePrefix = !prefix.IsNullOrEmpty() && eligibilityCode.StartsWith(prefix);
        DateTime today = DateTime.UtcNow;
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
        if (isEligiblePrefix) { 
            wfEvent.ValidityStartDate = today.AddDays(-1);
            wfEvent.DiscretionaryValidityStartDate = today.AddDays(-1);
            wfEvent.ValidityEndDate = today.AddMonths(3);
            wfEvent.GracePeriodEndDate = today.AddMonths(6);
            wfEvent.SubmissionDate = new DateTime(today.Year, today.AddMonths(-1).Month, 25);
            wfEvent.ParentLastName = checkData.LastName ?? "TESTER";
              
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
        if (!string.IsNullOrEmpty(wfTestCodePrefix) &&
            checkData.EligibilityCode.StartsWith(wfTestCodePrefix))
        {
            wfEvent = await Generate_Test_Working_Families_EventRecord(checkData);
        }
        else if (_ecsGateway.UseEcsforChecksWF == "true")
        {
            SoapCheckResponse innerResult = await _ecsGateway.EcsWFCheck(checkData);

            result.Status =  convertEcsResultStatus(innerResult);
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
        else
        {
            wfEvent = await Check_Working_Families_EventRecord(checkData.DateOfBirth, checkData.EligibilityCode,
          checkData.NationalInsuranceNumber, checkData.LastName);
        }
        var wfCheckData = JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);
        if (wfEvent != null)
        {
            wfCheckData.ValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
            wfCheckData.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
            wfCheckData.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
            wfCheckData.LastName = wfEvent.ParentLastName;
            wfCheckData.SubmissionDate = wfEvent.SubmissionDate.ToString("yyyy-MM-dd");
        }
        
        result.CheckData = JsonConvert.SerializeObject(wfCheckData);

        if (result.Status != CheckEligibilityStatus.notFound)
        {
            //Get current date
            var currentDate = DateTime.UtcNow.Date;

            if ((currentDate >= wfEvent.ValidityStartDate && currentDate <= wfEvent.ValidityEndDate) ||
                (currentDate >= wfEvent.ValidityStartDate && currentDate <= wfEvent.GracePeriodEndDate))
            {
                result.Status = CheckEligibilityStatus.eligible;
            }
            else
            {
                result.Status = CheckEligibilityStatus.notEligible;
            }
        }

        result.EligibilityCheckHashID =
            await _hashGateway.Create(wfCheckData, result.Status, source, auditDataTemplate);
        result.Updated = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // manually update bulk check
        await UpdateBulkCheckStatusIfComplete(guid);
    }

    private async Task Process_StandardCheck(string guid, AuditData auditDataTemplate, EligibilityCheck? result,
        CheckProcessData checkData)
    {
        var source = ProcessEligibilityCheckSource.HMRC;
        var checkResult = CheckEligibilityStatus.parentNotFound;

        if (_configuration.GetValue<string>("TestData:LastName") == checkData.LastName)
        {
            checkResult = TestDataCheck(checkData.NationalInsuranceNumber, checkData.NationalAsylumSeekerServiceNumber);
            source = ProcessEligibilityCheckSource.TEST;
        }

        else
        {
            if (!checkData.NationalInsuranceNumber.IsNullOrEmpty())
            {
                checkResult = await HMRC_Check(checkData);
                if (checkResult == CheckEligibilityStatus.parentNotFound)
                {
                    if (_ecsGateway.UseEcsforChecks=="true")
                    {
                        checkResult = await EcsCheck(checkData);
                        
                        source = ProcessEligibilityCheckSource.ECS;
                    }
                    else if(_ecsGateway.UseEcsforChecks=="false")
                    {
                        
                        checkResult = await DwpCitizenCheck(checkData, checkResult);
                        
                        source = ProcessEligibilityCheckSource.DWP;
                    }
                    else
                    {
                        checkResult = await EcsCheck(checkData);
                        source = ProcessEligibilityCheckSource.DWP;

                        if (await DwpCitizenCheck(checkData, checkResult) != checkResult)
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
            await _db.SaveChangesAsync();
        }

        // manually update bulk check
        await UpdateBulkCheckStatusIfComplete(guid);

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

    private async Task<CheckEligibilityStatus> EcsCheck(CheckProcessData data)
    {
        //check for benefit
        var result = await _ecsGateway.EcsCheck(data, data.Type);
        return convertEcsResultStatus(result);
    }


    private async Task<CheckEligibilityStatus> DwpCitizenCheck(CheckProcessData data,
        CheckEligibilityStatus checkResult)
    {
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
        // if a guid is not valid ie the request failed then the status is updated

        _logger.LogInformation($"Dwp before getting citizen");

        _logger.LogInformation(JsonConvert.SerializeObject(citizenRequest));
        var guid = await _dwpGateway.GetCitizen(citizenRequest, data.Type);
        _logger.LogInformation($"Dwp after getting citizen");
        if (guid.Length != 64)
        {
            _logger.LogInformation($"Dwp after getting citizen error " + guid);
            return (CheckEligibilityStatus)Enum.Parse(typeof(CheckEligibilityStatus), guid);
        }

        if (!string.IsNullOrEmpty(guid))
        {
            _logger.LogInformation($"Dwp has valid citizen");
            //check for benefit
            var result = await _dwpGateway.GetCitizenClaims(guid, DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
                DateTime.Now.ToString("yyyy-MM-dd"), data.Type);
            _logger.LogInformation($"Dwp after getting claim");

            if (result.StatusCode == StatusCodes.Status200OK)
            {
                checkResult = CheckEligibilityStatus.eligible;
                _logger.LogInformation($"Dwp is eligible");
            }
            else if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                checkResult = CheckEligibilityStatus.notEligible;
                _logger.LogInformation($"Dwp is not found");
            }
            else
            {
                _logger.LogError($"Dwp Error unknown Response status code:-{result.StatusCode}.");
                checkResult = CheckEligibilityStatus.error;
            }
        }

        return checkResult;
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
                        var result = await ProcessCheck(checkData.Guid, new AuditData
                        {
                            Type = AuditType.Check,
                            typeId = checkData.Guid,
                            authentication = queName,
                            method = "processQue",
                            source = "queueProcess",
                            url = "."
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
                            
                            // Update BulkCheck status if this was part of a bulk operation
                            await UpdateBulkCheckStatusIfComplete(checkData.Guid);
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

    /// <summary>
    /// Updates the BulkCheck status when all EligibilityChecks in a group are completed
    /// </summary>
    /// <param name="eligibilityCheckId">The ID of the eligibility check that was just processed</param>
    private async Task UpdateBulkCheckStatusIfComplete(string eligibilityCheckId)
    {
        try
        {
            // Get the eligibility check to find its group
            var eligibilityCheck = await _db.CheckEligibilities
                .FirstOrDefaultAsync(x => x.EligibilityCheckID == eligibilityCheckId && x.Status != CheckEligibilityStatus.deleted);

            if (eligibilityCheck?.Group == null)
                return; // Not part of a bulk operation

            // Get the corresponding BulkCheck
            var bulkCheck = await _db.BulkChecks
                .Include(x => x.EligibilityChecks)
                .FirstOrDefaultAsync(x => x.Guid == eligibilityCheck.Group && x.Status != BulkCheckStatus.Deleted);

            if (bulkCheck == null)
                return;

            // Check if all eligibility checks in this group are completed (exclude deleted records)
            var allEligibilityChecks = bulkCheck.EligibilityChecks.Where(x => x.Status != CheckEligibilityStatus.deleted);
            var pendingChecks = allEligibilityChecks
                .Where(x => x.Status == CheckEligibilityStatus.queuedForProcessing)
                .ToList();

            // If no pending checks, update bulk status to completed
            if (!pendingChecks.Any())
            {
                var hasErrors = allEligibilityChecks.Any(x => x.Status == CheckEligibilityStatus.error);
                
                bulkCheck.Status = hasErrors ? BulkCheckStatus.Failed : BulkCheckStatus.Completed;
                
                await _db.SaveChangesAsync();
                
                _logger.LogInformation("Updated BulkCheck {GroupId} status to {Status}", 
                    bulkCheck.Guid, bulkCheck.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating BulkCheck status for EligibilityCheck {EligibilityCheckId}", 
                eligibilityCheckId?.Replace(Environment.NewLine, "")?.Replace("\n", "")?.Replace("\r", ""));
            // Don't rethrow - we don't want to break the main processing flow
        }
    }

    #endregion
}