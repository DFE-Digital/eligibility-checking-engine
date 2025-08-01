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
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

    private readonly IDwpGateway _dwpGateway;
    private readonly IHash _hashGateway;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;

    public CheckEligibilityGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        QueueServiceClient queueClientGateway,
        IConfiguration configuration, IDwpGateway dwpGateway, IAudit audit, IHash hashGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _mapper = mapper;
        _dwpGateway = dwpGateway;
        _audit = audit;
        _hashGateway = hashGateway;
        _configuration = configuration;

        setQueueStandard(_configuration.GetValue<string>("QueueFsmCheckStandard"), queueClientGateway);
        setQueueBulk(_configuration.GetValue<string>("QueueFsmCheckBulk"), queueClientGateway);
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

            if (data is CheckEligibilityRequestBulkData bulkDataItem)
            {
                item.ClientIdentifier = bulkDataItem.ClientIdentifier;
            }

            item.Group = _groupId;
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.Created = DateTime.UtcNow;
            item.Updated = DateTime.UtcNow;

            item.Status = CheckEligibilityStatus.queuedForProcessing;
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData);
            if (item.Type == CheckEligibilityType.WorkingFamilies)
            {

                var wfEvent = await Check_Working_Families_EventRecord(checkData.DateOfBirth, checkData.EligibilityCode, checkData.NationalInsuranceNumber, checkData.LastName);
                if (wfEvent != null)
                {

                    checkData.ValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
                    checkData.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
                    checkData.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
                    checkData.LastName = wfEvent.ParentLastName;
                    checkData.SubmissionDate = wfEvent.SubmissionDate.ToString("yyyy-MM-dd");
                }

            }
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
        (type == CheckEligibilityType.None || type == x.Type));
        if (result != null) return result.Status;
        return null;
    }

    public async Task<CheckEligibilityStatus?> ProcessCheck(string guid, AuditData auditDataTemplate)
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid);

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

    public async Task<T?> GetItem<T>(string guid, CheckEligibilityType type, bool isBatchRecord = false) where T : CheckEligibilityItem
    {
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid && 
            (type == CheckEligibilityType.None || type == x.Type)); //TODO: Check that this is LINQ safe
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
            .Where(x => x.Group == guid).ToList();
        if (resultList != null && resultList.Any())
        {
            var type = typeof(T);
            if (type == typeof(IList<CheckEligibilityItem>))
            {
                var sequence = 1;
                foreach (var result in resultList)
                {
                    var item = await GetItem<CheckEligibilityItem>(result.EligibilityCheckID, result.Type, isBatchRecord: true);
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
        var result = await _db.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid);
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
            .Where(x => x.Group == guid)
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

    public async Task<IEnumerable<BulkCheck>> GetBulkStatuses(string localAuthority)
    {
        var minDate = DateTime.Now.AddDays(-7);

        var allChecks = await _db.CheckEligibilities
            .Where(x => string.Equals(x.ClientIdentifier, localAuthority) && x.Created > minDate &&
                        !string.IsNullOrWhiteSpace(x.Group))
            .ToListAsync();

        var results = allChecks
            .GroupBy(b => b.Group)
            .Select(g =>
            {
                var statuses = g.Select(x => x.Status);

                var allQueued = statuses.All(s => s == CheckEligibilityStatus.queuedForProcessing);
                var allCompleted = statuses.All(s => s != CheckEligibilityStatus.queuedForProcessing);

                string status;
                if (allQueued)
                    status = "NotStarted";
                else if (allCompleted)
                    status = "Complete";
                else
                    status = "InProgress";

                var any = g.First();

                return new BulkCheck
                {
                    Guid = g.Key,
                    EligibilityType = any.Type.ToString(),
                    SubmittedDate = any.Created,
                    Status = status
                };
            });

        return results;
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

    private async Task<WorkingFamiliesEvent> Check_Working_Families_EventRecord(string dateOfBirth, string eligibilityCode, string nino, string lastName)
    {

        DateTime checkDob = DateTime.ParseExact(dateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var wfEvent = await _db.WorkingFamiliesEvents.FirstOrDefaultAsync(x =>
         x.EligibilityCode == eligibilityCode &&
        (x.ParentNationalInsuranceNumber == nino || x.PartnerNationalInsuranceNumber == nino) &&
        (x.ParentLastName.ToUpper() == lastName || x.PartnerLastName.ToUpper() == lastName) &&
        x.ChildDateOfBirth == checkDob);

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
        var source = ProcessEligibilityCheckSource.HMRC;

        var wfEvent = await Check_Working_Families_EventRecord(checkData.DateOfBirth, checkData.EligibilityCode, checkData.NationalInsuranceNumber, checkData.LastName);
        var wfCheckData = JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);
        if (wfEvent != null)
        {

            wfCheckData.ValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
            wfCheckData.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
            wfCheckData.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
            wfCheckData.LastName = wfEvent.ParentLastName;
            wfCheckData.SubmissionDate = wfEvent.SubmissionDate.ToString("yyyy-MM-dd");
            result.CheckData = JsonConvert.SerializeObject(wfCheckData);

            //Get current date
            var currentDate = DateTime.UtcNow.Date;

            if ((currentDate >= wfEvent.DiscretionaryValidityStartDate && currentDate <= wfEvent.ValidityEndDate) ||
                (currentDate >= wfEvent.DiscretionaryValidityStartDate && currentDate <= wfEvent.GracePeriodEndDate))
            {
                result.Status = CheckEligibilityStatus.eligible;
            }
            else
            {
                result.Status = CheckEligibilityStatus.notEligible;
            }

        }
        else
        {
            result.Status = CheckEligibilityStatus.notFound;
        }

        result.EligibilityCheckHashID =
                    await _hashGateway.Create(wfCheckData, result.Status, source, auditDataTemplate);
        result.Updated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
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
                    checkResult = await DWP_Check(checkData);
                    source = ProcessEligibilityCheckSource.DWP;
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

    private async Task<CheckEligibilityStatus> DWP_Check(CheckProcessData data)
    {
        var checkResult = CheckEligibilityStatus.parentNotFound;
        _logger.LogInformation($"Dwp check use ECS service:- {_dwpGateway.UseEcsforChecks}");
        if (!_dwpGateway.UseEcsforChecks)
            checkResult = await DwpCitizenCheck(data, checkResult);
        else
            checkResult = await DwpEcsFsmCheck(data, checkResult);

        return checkResult;
    }


    private async Task<CheckEligibilityStatus> DwpEcsFsmCheck(CheckProcessData data, CheckEligibilityStatus checkResult)
    {
        //check for benefit
        var result = await _dwpGateway.EcsFsmCheck(data);
        if (result != null)
        {
            if (result.Status == "1")
            {
                checkResult = CheckEligibilityStatus.eligible;
            }
            else if (result.Status == "0" && result.ErrorCode == "0" && result.Qualifier.IsNullOrEmpty())
            {
                checkResult = CheckEligibilityStatus.notEligible;
            }
            else if (result.Status == "0" && result.ErrorCode == "0" && result.Qualifier == "No Trace - Check data")
            {
                checkResult = CheckEligibilityStatus.parentNotFound;
            }
            else
            {
                _logger.LogError(
                    $"Error unknown Response status code:-{result.Status}, error code:-{result.ErrorCode} qualifier:-{result.Qualifier}");
                checkResult = CheckEligibilityStatus.error;
            }
        }
        else
        {
            _logger.LogError("Error ECS unknown Response of null");
            checkResult = CheckEligibilityStatus.error;
        }

        return checkResult;
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