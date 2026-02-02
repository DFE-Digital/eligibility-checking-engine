// Ignore Spelling: Fsm

using Azure.Storage.Queues;
using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace CheckYourEligibility.API.Gateways;

public class CheckingEngineGateway : ICheckingEngine
{
    private const int SurnameCheckCharachters = 3;
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;

    private readonly IEcsAdapter _ecsAdapter;
    private readonly IDwpAdapter _dwpAdapter;
    private readonly IHash _hashGateway;
    private readonly ILogger _logger;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;

    public CheckingEngineGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext,
        IConfiguration configuration, IEcsAdapter ecsAdapter, IDwpAdapter dwpAdapter, IHash hashGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _ecsAdapter = ecsAdapter;
        _dwpAdapter = dwpAdapter;
        _hashGateway = hashGateway;
        _configuration = configuration;
    }

    public async Task<CheckEligibilityStatus?> ProcessCheckAsync(string guid, AuditData auditDataTemplate, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        //TODO: This should come from the other gateway
        var result = await context.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid &&
                                                                           x.Status != CheckEligibilityStatus.deleted);

        if (result != null)
        {
            var checkData = GetCheckProcessData(result.Type, result.CheckData);
            if (result.Status != CheckEligibilityStatus.queuedForProcessing)
                return result.Status;
            
            //TODO: This should live in the use case
            switch (result.Type)
            {
                case CheckEligibilityType.FreeSchoolMeals:
                case CheckEligibilityType.TwoYearOffer:
                case CheckEligibilityType.EarlyYearPupilPremium:
                {
                    await Process_StandardCheck(guid, auditDataTemplate, result, checkData, dbContextFactory);
                }
                    break;
                case CheckEligibilityType.WorkingFamilies:
                {
                    await Process_WorkingFamilies_StandardCheck(guid, auditDataTemplate, result, checkData, dbContextFactory);
                }
                    break;
            }

            return result.Status;
        }

        return null;
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

    /// <summary>
    /// Logic to find a match in Working families events' table
    /// Checks if record with the same EligibilityCode-ParentNINO-ChildDOB-ParentLastName exists in the WorkingFamiliesEvents Table
    /// </summary>
    /// <param name="checkData"></param>
    /// <returns></returns>
    private async Task<WorkingFamiliesEvent> Check_Working_Families_EventRecord(string dateOfBirth,
        string eligibilityCode, string nino, string lastName, EligibilityCheckContext dbContextFactory = null )
    {
        //TODO: This should probably be its own adapter
        var context = dbContextFactory ?? _db ;
        DateTime checkDob = DateTime.ParseExact(dateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var wfRecords = await context.WorkingFamiliesEvents.Where(x =>
            x.EligibilityCode == eligibilityCode &&
            (x.ParentNationalInsuranceNumber == nino || x.PartnerNationalInsuranceNumber == nino) &&
            (lastName == null || lastName == "" || x.ParentLastName.ToUpper() == lastName ||
             x.PartnerLastName.ToUpper() == lastName) &&
            x.ChildDateOfBirth == checkDob).OrderByDescending(x => x.SubmissionDate).AsNoTracking().ToListAsync();

        WorkingFamiliesEvent wfEvent = wfRecords.FirstOrDefault();
        // If there is more than one record
        // check if second to last record has not expired yet
        // set the event to the second record that is still valid, sets submission date
        // and get set ValidityEndDate and the GracePeriodEndDate of the future record
        if (wfRecords.Count() > 1 && wfRecords[1].ValidityEndDate > DateTime.UtcNow)
        {
            wfEvent = wfRecords[1];
            wfEvent.ValidityEndDate = wfRecords[0].ValidityEndDate;
            wfEvent.GracePeriodEndDate = wfRecords[0].GracePeriodEndDate;
        }

        //Check for contiguous events and set VSD to earliest VSD of the current contiguous block
        for (int i=0; i < wfRecords.Count() - 1; i++)
        {
            if (wfRecords[i].DiscretionaryValidityStartDate <= wfRecords[i+1].GracePeriodEndDate)
            {
                wfEvent.DiscretionaryValidityStartDate = wfRecords[i+1].DiscretionaryValidityStartDate;
                wfEvent.ValidityStartDate = wfRecords[i+1].ValidityStartDate;
            }
            else
            {
                break;
            }
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
        EligibilityCheck? result, CheckProcessData checkData, EligibilityCheckContext dbContextFactory = null)
    {
        //TODO: This should be cleaned up
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
        var source = ProcessEligibilityCheckSource.HMRC;
        string wfTestCodePrefix = _configuration.GetValue<string>("TestData:WFTestCodePrefix");

        result.Status = CheckEligibilityStatus.notFound;
        
        var sw = Stopwatch.StartNew();

        // Get event for test record
        if (!string.IsNullOrEmpty(wfTestCodePrefix) &&
            checkData.EligibilityCode.StartsWith(wfTestCodePrefix))
        {
            wfEvent = await Generate_Test_Working_Families_EventRecord(checkData);
        }

        // Get event for ecs record
        else if (_ecsAdapter.UseEcsforChecksWF == "true")
        {
            //To ensure correct LA ID is passed when using ECS for checks
            string laId = ExtractLAIdFromScope(auditDataTemplate.scope);
            SoapCheckResponse innerResult = await _ecsAdapter.EcsWFCheck(checkData, laId);

            result.Status = convertEcsResultStatus(innerResult, CheckEligibilityType.WorkingFamilies);
            if (result.Status != CheckEligibilityStatus.notFound && result.Status != CheckEligibilityStatus.error)
            {
                wfEvent.EligibilityCode = checkData.EligibilityCode;
                wfEvent.ParentLastName = checkData.LastName;  //Return value as submitted in request
                wfEvent.DiscretionaryValidityStartDate = DateTime.Parse(innerResult.ValidityStartDate);
                wfEvent.ValidityEndDate = DateTime.Parse(innerResult.ValidityEndDate);
                wfEvent.GracePeriodEndDate = DateTime.Parse(innerResult.GracePeriodEndDate);
            }

            source = ProcessEligibilityCheckSource.ECS;
            
            _logger.LogInformation($"Processing ECS WF check in {sw.ElapsedMilliseconds} ms");
        }

        // Get event for ECE record
        else
        {
            wfEvent = await Check_Working_Families_EventRecord(checkData.DateOfBirth, checkData.EligibilityCode,
                checkData.NationalInsuranceNumber, checkData.LastName);
            
            _logger.LogInformation($"Processing ECE WF check in {sw.ElapsedMilliseconds} ms");
        }

        var wfCheckData = JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);
        
        // If event is returned initiate business logic. 
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
            await _hashGateway.Create(wfCheckData, result.Status, source, auditDataTemplate, dbContextFactory);

        var context = dbContextFactory ?? _db;
        // Now update the check data in the EligibilityCheckTable with all the neccessary fields
        // that needs to be returned on the GET request if a record has been found
        if (wfEvent != null && result.Status != CheckEligibilityStatus.notFound)
        {          
            wfCheckData.ValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
            wfCheckData.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
            wfCheckData.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
            wfCheckData.LastName = wfEvent.ParentLastName;
            wfCheckData.SubmissionDate = wfEvent.SubmissionDate.ToString("yyyy-MM-dd");

            result.CheckData = JsonConvert.SerializeObject(wfCheckData);
            context.CheckEligibilities.Update(result);
        }

        result.Updated = DateTime.UtcNow;
        await context.SaveChangesAsync();
      
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
        CheckProcessData checkData, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var source = ProcessEligibilityCheckSource.HMRC;
        var checkResult = CheckEligibilityStatus.parentNotFound;
        CAPIClaimResponse capiClaimResponse = new();
        // Variables needed for ECS conflict records
        var eceCheckResult = CheckEligibilityStatus.parentNotFound;

        // For CAPI request to track request conflicts from DWP side
        string correlationId = Guid.NewGuid().ToString();

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
                checkResult = await HMRC_Check(checkData, dbContextFactory);
                if (checkResult == CheckEligibilityStatus.parentNotFound)
                {
                    var sw = Stopwatch.StartNew();
                    
                    //TODO: This should live in the use case
                    if (_ecsAdapter.UseEcsforChecks == "true")
                    {
                        checkResult = await EcsCheck(checkData, laId);
                        source = ProcessEligibilityCheckSource.ECS;
                        _logger.LogInformation($"Processing ECS check in {sw.ElapsedMilliseconds} ms");
                    }
                    else if (_ecsAdapter.UseEcsforChecks == "false")
                    {

                        capiClaimResponse = await DwpCitizenCheck(checkData, checkResult, correlationId);
                        checkResult = capiClaimResponse.CheckEligibilityStatus;
                        source = ProcessEligibilityCheckSource.DWP;
                        _logger.LogInformation($"Processing ECE check in {sw.ElapsedMilliseconds} ms");
                    }
                    else // do both checks
                    {
                        checkResult = await EcsCheck(checkData, laId);
                        source = ProcessEligibilityCheckSource.DWP;
                        _logger.LogInformation($"Processing ECS check in {sw.ElapsedMilliseconds} ms");

                        sw.Restart();
                        capiClaimResponse = await DwpCitizenCheck(checkData, checkResult, correlationId);
                        eceCheckResult = capiClaimResponse.CheckEligibilityStatus;
                        _logger.LogInformation($"Processing ECE check in {sw.ElapsedMilliseconds} ms");

                        if (checkResult != eceCheckResult)
                        {
                            source = ProcessEligibilityCheckSource.ECS_CONFLICT;
                        }

                    }

                }
            }
            else if (!checkData.NationalAsylumSeekerServiceNumber.IsNullOrEmpty())
            {
                checkResult = await HO_Check(checkData, dbContextFactory);
                source = ProcessEligibilityCheckSource.HO;
            }
        }

        result.Status = checkResult;
        result.Updated = DateTime.UtcNow;

        if (checkResult == CheckEligibilityStatus.error)
        {
            // Revert status back and do not save changes
            result.Status = CheckEligibilityStatus.queuedForProcessing;
        }
        else
        {
            result.EligibilityCheckHashID =
               await _hashGateway.Create(checkData, checkResult, source, auditDataTemplate, dbContextFactory);

            //If CAPI returns a different result from ECS
            // Create a record
            if (source == ProcessEligibilityCheckSource.ECS_CONFLICT)
            {
                var organisation = await _db.Audits.FirstOrDefaultAsync(a => a.TypeID == guid);
                ECSConflict ecsConflictRecord = new()
                {
                    CorrelationID = correlationId,
                    ECE_Status = eceCheckResult,
                    ECS_Status = checkResult,
                    DateOfBirth = checkData.DateOfBirth,
                    LastName = checkData.LastName,
                    Nino = checkData.NationalInsuranceNumber,
                    Type = checkData.Type,
                    TimeStamp = DateTime.UtcNow,
                    EligibilityCheckHashID = result.EligibilityCheckHashID,
                    CAPIEndpoint = capiClaimResponse.CAPIEndpoint,
                    Reason = capiClaimResponse.Reason,
                    CAPIResponseCode = capiClaimResponse.CAPIResponseCode


                };
                await context.ECSConflicts.AddAsync(ecsConflictRecord);

            }

            await context.SaveChangesAsync();
        }

        var processingTime = (DateTime.Now.ToUniversalTime() - result.Created.ToUniversalTime()).Seconds;
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

    //TODO: These two could be adapters
    private async Task<CheckEligibilityStatus> HO_Check(CheckProcessData data, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var checkReults = context.FreeSchoolMealsHO.Where(x =>
                x.NASS == data.NationalAsylumSeekerServiceNumber
                && x.DateOfBirth == DateTime.ParseExact(data.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None))
            .Select(x => x.LastName);

        return CheckSurname(data.LastName, checkReults);
    }

    private async Task<CheckEligibilityStatus> HMRC_Check(CheckProcessData data, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var checkReults = context.FreeSchoolMealsHMRC.Where(x =>
                x.FreeSchoolMealsHMRCID == data.NationalInsuranceNumber
                && x.DateOfBirth == DateTime.ParseExact(data.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None))
            .Select(x => x.Surname);

        return CheckSurname(data.LastName, checkReults);
    }

    private CheckEligibilityStatus convertEcsResultStatus(SoapCheckResponse? result, CheckEligibilityType checkType = CheckEligibilityType.None)
    {
        if (result != null)
        {
            if (result.Status == "1")
            {
                return CheckEligibilityStatus.eligible;
            }

            else if (checkType != CheckEligibilityType.WorkingFamilies && result.Status == "0" && result.ErrorCode == "0" &&
                     ( string.IsNullOrEmpty(result.Qualifier) || result.Qualifier.ToUpper() == "PENDING - KEEP CHECKING" || result.Qualifier.ToUpper() == "MANUAL PROCESS"))
            {
                return CheckEligibilityStatus.notEligible;
            }
            // Since WF checks can only return Qualifier that is empty, or a "Discretionary Start" on Status 1 (eligible)
            // We need to check the type of the check before setting status as notFound/notligible status response from ECS is different between WF and the rest of the checks
            else if (checkType == CheckEligibilityType.WorkingFamilies && result.Status == "0" && result.ErrorCode == "0" && string.IsNullOrEmpty(result.Qualifier)) {

                if (string.IsNullOrEmpty(result.ValidityStartDate) && string.IsNullOrEmpty(result.ValidityEndDate) && string.IsNullOrEmpty(result.GracePeriodEndDate))
                {
                    return CheckEligibilityStatus.notFound;
                }
                else 
                {
                    return CheckEligibilityStatus.notEligible;
                }
                                
            }

            else if (result.Qualifier.ToUpper() == "NO TRACE - CHECK DATA" && result.Status == "0" && result.ErrorCode == "0")
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
        var result = await _ecsAdapter.EcsCheck(data, data.Type, LaId);
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
        var citizenResponse = await _dwpAdapter.GetCitizen(citizenRequest, data.Type, correlationId);
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
            var result = await _dwpAdapter.GetCitizenClaims(citizenResponse.Guid, DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
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

    #endregion
}