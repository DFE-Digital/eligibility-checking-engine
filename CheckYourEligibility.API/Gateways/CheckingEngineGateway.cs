using Azure.Storage.Queues;
using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Factories;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Helpers;
using CheckYourEligibility.API.Services;
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
    private readonly ILocalAuthority _localAuthority;
    private readonly IDwpAdapter _dwpAdapter;
    private readonly IHash _hashGateway;
    private readonly ILogger _logger;
    private readonly IEligibilityPolicy _eligibilityPolicy;
    private readonly IWorkingFamiliesTestScenarioFactory _workingFamiliesTestScenarioFactory;
    private readonly IStandardCheckTestScenarioFactory _standardCheckTestScenarioFactory;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;

    private readonly string isEligiblePrefix;
    private readonly string isInGracePeriodPrefix;
    private readonly string isNotYetEligiblePrefix;
    private readonly string isExpiredPrefix;
    private readonly Dictionary<CheckEligibilityType, double> _DWP_ApiUniversalCreditThreshold = new();
    private readonly Dictionary<CheckEligibilityType, string> _DWP_ApiCriteria = new();
    public CheckingEngineGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext,
        IConfiguration configuration,
        IEcsAdapter ecsAdapter, 
        IDwpAdapter dwpAdapter, 
        IHash hashGateway,
        ILocalAuthority 
        localAuthority, 
        IEligibilityPolicy eligibilityPolicy,
        IWorkingFamiliesTestScenarioFactory workingFamiliesTestScenarioFactory,
        IStandardCheckTestScenarioFactory standardCheckTestScenarioFactory)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _ecsAdapter = ecsAdapter;
        _dwpAdapter = dwpAdapter;
        _hashGateway = hashGateway;
        _configuration = configuration;
        _localAuthority = localAuthority;
        _eligibilityPolicy = eligibilityPolicy;
        _workingFamiliesTestScenarioFactory = workingFamiliesTestScenarioFactory;
        _standardCheckTestScenarioFactory = standardCheckTestScenarioFactory;

        isEligiblePrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible");
        isInGracePeriodPrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:InGracePeriod");
        isNotYetEligiblePrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:NotYetEligible");
        isExpiredPrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Expired");

        // Use new DefaultEligibilityPolicies config section
        var defaultPoliciesSection = _configuration.GetSection("Dwp:DefaultEligibilityPolicies");

        if (defaultPoliciesSection.Exists())
        {
            foreach (var type in new[] { CheckEligibilityType.FreeSchoolMeals, CheckEligibilityType.EarlyYearPupilPremium, CheckEligibilityType.TwoYearOffer })
            {
                var typeName = type.ToString();
                var policySection = defaultPoliciesSection.GetSection(typeName);
                if (policySection.Exists())
                {
                    var thresholdStr = policySection["Threshold"];
                    if (double.TryParse(thresholdStr, out var threshold))
                        _DWP_ApiUniversalCreditThreshold[type] = threshold;
                    var criteria = policySection["Criteria"];
                    if (!string.IsNullOrEmpty(criteria))
                        _DWP_ApiCriteria[type] = criteria;
                }
            }
        }
    }
    public async Task<(CheckEligibilityStatus?, EligibilityTier?)> ProcessCheckAsync(string guid, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        //TODO: This should come from the other gateway
        var result = await context.CheckEligibilities.FirstOrDefaultAsync(x => x.EligibilityCheckID == guid &&
                                                                          x.IsDeleted == false);

        if (result != null)
        {

            var checkData = MapCheckDataHelper.MapCheckDataBasedOnType(result.Type, result.CheckData);
            //TODO: This should live in the use case
            switch (result.Type)
            {
                case CheckEligibilityType.FreeSchoolMeals:
                case CheckEligibilityType.TwoYearOffer:
                case CheckEligibilityType.EarlyYearPupilPremium:
                    {
                        await Process_StandardCheck(result, checkData, dbContextFactory);
                    }
                    break;
                case CheckEligibilityType.WorkingFamilies:
                    {
                        await Process_WorkingFamilies_StandardCheck(result, checkData, dbContextFactory);
                    }
                    break;
            }

            return (result.Status, result.Tier);
        }

        return (null, null);
    }

    #region Private
    /// <summary>
    /// Logic to find a match in Working families events' table
    /// Checks if record with the same EligibilityCode-ParentNINO-ChildDOB-ParentLastName exists in the WorkingFamiliesEvents Table
    /// </summary>
    /// <param name="checkData"></param>
    /// <returns></returns>
    private async Task<WorkingFamiliesEvent> Check_Working_Families_EventRecord(string dateOfBirth,
        string eligibilityCode, string nino, string lastName, EligibilityCheckContext dbContextFactory = null)
    {
        //TODO: This should probably be its own adapter
        var context = dbContextFactory ?? _db;
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
        for (int i = 0; i < wfRecords.Count() - 1; i++)
        {
            if (wfRecords[i].DiscretionaryValidityStartDate <= wfRecords[i + 1].GracePeriodEndDate)
            {
                wfEvent.DiscretionaryValidityStartDate = wfRecords[i + 1].DiscretionaryValidityStartDate;
                wfEvent.ValidityStartDate = wfRecords[i + 1].ValidityStartDate;
            }
            else
            {
                break;
            }
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
    private async Task Process_WorkingFamilies_StandardCheck(EligibilityCheck? result, CheckProcessData checkData, EligibilityCheckContext dbContextFactory = null)
    {
        //TODO: This should be cleaned up
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
        var source = ProcessEligibilityCheckSource.HMRC;
        string wfTestCodePrefix = _configuration.GetValue<string>("TestData:WFTestCodePrefix");

        var sw = Stopwatch.StartNew();

   
        if (!string.IsNullOrEmpty(wfTestCodePrefix))
        {
            // Get event for TEST record client side
            if (checkData.EligibilityCode.StartsWith(wfTestCodePrefix)) {
                wfEvent = _workingFamiliesTestScenarioFactory.GenerateTestScenarioClientSide(checkData);
            }
            // Get event for TEST record internal side
            else if (checkData.EligibilityCode.StartsWith("7"))
            {
                wfEvent = _workingFamiliesTestScenarioFactory.GenerateTestScenarioInternalSide(checkData);
            }

            if (wfEvent == null) { result.Status = CheckEligibilityStatus.notFound; }
        }
       
        // Get event for ECS record
        else if (_ecsAdapter.UseEcsforChecksWF == "true")
        {
            //To ensure correct LA ID is passed when using ECS for checks
            string laId = EligibilityCheckHelper.GetOrganisationIdOFTypeLocalAuthority(result.OrganisationType, result.OrganisationID);
            SoapCheckResponse innerResult = await _ecsAdapter.EcsWFCheck(checkData, laId);

            result.Status = convertEcsResultStatus(innerResult, CheckEligibilityType.WorkingFamilies);

            if (result.Status != CheckEligibilityStatus.notFound && result.Status != CheckEligibilityStatus.error)
            {
                wfEvent.EligibilityCode = checkData.EligibilityCode;
                wfEvent.ParentLastName = checkData.LastName;  //Return value as submitted in request
                wfEvent.DiscretionaryValidityStartDate = DateTime.Parse(innerResult.ValidityStartDate);
                wfEvent.ValidityStartDate = DateTime.Parse(innerResult.ValidityStartDate);
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
                checkData.NationalInsuranceNumber, checkData.LastName, dbContextFactory);

            if (wfEvent == null) { result.Status = CheckEligibilityStatus.notFound; }

            _logger.LogInformation($"Processing ECE WF check in {sw.ElapsedMilliseconds} ms");
        }

        var wfCheckData = JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);
        // If event is returned initiate business logic.
        if (wfEvent != null && result.Status != CheckEligibilityStatus.error && result.Status != CheckEligibilityStatus.notFound)
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
            await _hashGateway.Create(wfCheckData, result.Status, result.Tier, source, dbContextFactory);

        var context = dbContextFactory ?? _db;
        // Now update the check data in the EligibilityCheckTable with all the neccessary fields
        // that needs to be returned on the GET request if a record has been found
        if (wfEvent != null && result.Status != CheckEligibilityStatus.error && result.Status != CheckEligibilityStatus.notFound)
        {
            wfCheckData.DiscretionaryValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
            wfCheckData.ValidityStartDate = wfEvent.ValidityStartDate.ToString("yyyy-MM-dd");
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

    private async Task<EligibilityPolicy> GetOrganisationEligibilityPolicyAsync(string organisationType, int? orgId, CheckEligibilityType type, EligibilityCheckContext dbContextFactory = null)
    {

        if (organisationType == Domain.Constants.OrganisationType.local_authority && orgId is int LaId && LaId != 0)
        {
            int policyID = await _localAuthority.GetEligibilityPolicyIdForTypeAsync(LaId, type, dbContextFactory);
            // get policy for the LA          
            if (policyID != 0)
                return await _eligibilityPolicy.GeEligibilityPolicyByIdAsync(policyID, dbContextFactory);
        }


        //fallback to default policy from appsettings.
        return new EligibilityPolicy
        {
            CheckType = type,
            EligibilityCriteria = Enum.Parse<EligibilityCriteria>(_DWP_ApiCriteria[type]),
            UniversalCreditThreshold = _DWP_ApiUniversalCreditThreshold[type],
            IsDeleted = false
        };

    }
    private async Task Process_StandardCheck(EligibilityCheck result,
        CheckProcessData checkData, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var source = ProcessEligibilityCheckSource.HMRC;
        var checkStatusResult = CheckEligibilityStatus.parentNotFound;
        EligibilityTier? checkTierResult = null;
        CAPIClaimResponseBase capiClaimResponse = new();
        // Variables needed for ECS conflict records
        var eceCheckResult = CheckEligibilityStatus.parentNotFound;

        // For CAPI request to track request conflicts from DWP side
        string correlationId = Guid.NewGuid().ToString();

        if (_configuration.GetValue<string>("TestData:LastName") == checkData.LastName)
        {
            var(testStatus, testTier) = _standardCheckTestScenarioFactory.TestDataCheck(checkData.NationalInsuranceNumber, checkData.NationalAsylumSeekerServiceNumber, result.Type);
            checkStatusResult = testStatus;
            checkTierResult = testTier;
            source = ProcessEligibilityCheckSource.TEST;
        }
        else
        {

            if (!checkData.NationalInsuranceNumber.IsNullOrEmpty())
            {

                var eligibilityPolicy = await GetOrganisationEligibilityPolicyAsync(result.OrganisationType, result.OrganisationID, result.Type, dbContextFactory);
                //To ensure correct LA ID is passed when using ECS for checks

                string localAuthorityId = EligibilityCheckHelper.GetOrganisationIdOFTypeLocalAuthority(result.OrganisationType, result.OrganisationID);

                checkStatusResult = await HMRC_Check(checkData, dbContextFactory);
                if (checkStatusResult == CheckEligibilityStatus.parentNotFound)
                {
                    var sw = Stopwatch.StartNew();

                    //TODO: This should live in the use case
                    if (_ecsAdapter.UseEcsforChecks == "true")
                    {
                        checkStatusResult = await EcsCheck(checkData, localAuthorityId);
                        source = ProcessEligibilityCheckSource.ECS;
                        _logger.LogInformation($"Processing ECS check in {sw.ElapsedMilliseconds} ms");
                    }
                    else if (_ecsAdapter.UseEcsforChecks == "false")
                    {

                        capiClaimResponse = await DwpCitizenCheck(checkData, checkStatusResult, correlationId, eligibilityPolicy);

                        checkStatusResult = capiClaimResponse.CheckEligibilityStatus;
                        checkTierResult = capiClaimResponse.EligibilityTier;
                        checkData.ErrorCode = capiClaimResponse.ErrorCode;                        

                        source = ProcessEligibilityCheckSource.DWP;

                        var capiAudit = new CAPIAudit(
                              Guid.Parse(result.EligibilityCheckID),
                              Guid.Parse(correlationId),
                              capiClaimResponse.CAPIEndpoint,
                              capiClaimResponse.RequestBody,
                              capiClaimResponse.ResponseBody,
                              capiClaimResponse.ResponseCode,
                              capiClaimResponse.CAPIResponseCode);

                        try
                        {
                            await context.CAPIAudits.AddAsync(capiAudit);
                            await context.SaveChangesAsync();
                        }
                        catch (Exception ex) {

                            _logger.LogError(ex," Check:{checkId} Action:AddToCAPIAudits Status:Failed", result.EligibilityCheckID);
                        } 

                        _logger.LogInformation($"Processing ECE check in {sw.ElapsedMilliseconds} ms");

                    }
                    else // do both checks
                    {
                        checkStatusResult = await EcsCheck(checkData, localAuthorityId);
                        source = ProcessEligibilityCheckSource.DWP;
                        _logger.LogInformation($"Processing ECS check in {sw.ElapsedMilliseconds} ms");

                        sw.Restart();
                        capiClaimResponse = await DwpCitizenCheck(checkData, checkStatusResult, correlationId, eligibilityPolicy);
                        eceCheckResult = capiClaimResponse.CheckEligibilityStatus;
                        _logger.LogInformation($"Processing ECE check in {sw.ElapsedMilliseconds} ms");

                        if (checkStatusResult != eceCheckResult)
                        {
                            source = ProcessEligibilityCheckSource.ECS_CONFLICT;
                        }

                    }

                }
            }
            else if (!checkData.NationalAsylumSeekerServiceNumber.IsNullOrEmpty())
            {
                checkStatusResult = await HO_Check(checkData, dbContextFactory);
                source = ProcessEligibilityCheckSource.HO;

                if (checkStatusResult == CheckEligibilityStatus.eligible)
                {
                    checkTierResult = EligibilityTier.targeted;
                }
            }
        }

        if (result.Type == CheckEligibilityType.FreeSchoolMeals && checkStatusResult == CheckEligibilityStatus.eligible)
        {
            checkData.EligibilityEndDate = (EligibilityCheckHelper.GetEligibilityEndDateFSM(result.Created)).ToString("yyyy-MM-dd");            
        }

        result.Status = checkStatusResult;
        result.Tier = checkTierResult;
        result.Updated = DateTime.UtcNow;

        if (checkStatusResult == CheckEligibilityStatus.error &&
            string.IsNullOrWhiteSpace(checkData.ErrorCode))
        {
            checkData.ErrorCode = "STE50";
        }

        result.CheckData = JsonConvert.SerializeObject(checkData);

        if (checkStatusResult == CheckEligibilityStatus.error)
        {
            // map 422 to not found here
            result.Status = capiClaimResponse.ResponseCode == HttpStatusCode.UnprocessableEntity
                ? CheckEligibilityStatus.parentNotFound
                : CheckEligibilityStatus.queuedForProcessing;
        }
        else
        {
            result.EligibilityCheckHashID =
               await _hashGateway.Create(checkData, checkStatusResult, result.Tier, source, dbContextFactory);

            // If CAPI returns a different result from ECS
            // Create a record
            if (source == ProcessEligibilityCheckSource.ECS_CONFLICT)
            {
                ECSConflict ecsConflictRecord = new()
                {
                    CorrelationID = correlationId,
                    ECE_Status = eceCheckResult,
                    ECS_Status = checkStatusResult,
                    DateOfBirth = checkData.DateOfBirth,
                    LastName = checkData.LastName,
                    Nino = checkData.NationalInsuranceNumber,
                    Type = checkData.Type,
                    TimeStamp = DateTime.UtcNow,
                    EligibilityCheckHashID = result.EligibilityCheckHashID,
                    CAPIEndpoint = capiClaimResponse.CAPIEndpoint,
                    Reason = capiClaimResponse.Reason,
                    CAPIResponseCode = capiClaimResponse.ResponseCode

                };
                await context.ECSConflicts.AddAsync(ecsConflictRecord);

            }
        }
        await context.SaveChangesAsync();

        var processingTime = (DateTime.Now.ToUniversalTime() - result.Created.ToUniversalTime()).Seconds;
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
                     (string.IsNullOrEmpty(result.Qualifier) || result.Qualifier.ToUpper() == "PENDING - KEEP CHECKING" || result.Qualifier.ToUpper() == "MANUAL PROCESS"))
            {
                return CheckEligibilityStatus.notEligible;
            }
            // Since WF checks can only return Qualifier that is empty, or a "Discretionary Start" on Status 1 (eligible)
            // We need to check the type of the check before setting status as notFound/notligible status response from ECS is different between WF and the rest of the checks
            else if (checkType == CheckEligibilityType.WorkingFamilies && result.Status == "0" && result.ErrorCode == "0" && string.IsNullOrEmpty(result.Qualifier))
            {

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

    public async Task<CAPIClaimResponseBase> DwpCitizenCheck(CheckProcessData data,
        CheckEligibilityStatus checkResult, string correlationId, EligibilityPolicy eligibilityPolicy)
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
        _logger.LogInformation(JsonConvert.SerializeObject(citizenRequest));
        var citizenResponse = await _dwpAdapter.GetCitizen(citizenRequest, data.Type, correlationId);

        if (string.IsNullOrEmpty(citizenResponse.Guid))
        {
            _logger.LogInformation("Dwp after not finding citizen ResponseStatusCode:{code}, \n" +
                "DWPcorrelationId: {correlationId} \n" +
                "NINO:{nino} \n" +
                "LastName:{lastName}\n" +
                "DateOfBirth:{dateOfBirth}",
                citizenResponse.ResponseCode,
                correlationId,
                data.NationalInsuranceNumber,
                data.LastName,
                data.DateOfBirth);
            return citizenResponse;
        }
        // Guid returned = citizen found
        else
        {
            _logger.LogInformation("Dwp has valid citizen, correlationId:{correlationId}", correlationId);

            // Perform a benefit check
            var result = await _dwpAdapter.GetCitizenClaims(citizenResponse.Guid, DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
                DateTime.Now.ToString("yyyy-MM-dd"), data.Type, correlationId, eligibilityPolicy);
            _logger.LogInformation("Dwp after getting claim,correlationId:{correlationId}", correlationId);

            if (result.ResponseCode == HttpStatusCode.OK)
            {
                result.CheckEligibilityStatus = CheckEligibilityStatus.eligible;
                _logger.LogInformation("Dwp is eligible correlationId:{correlationId}", correlationId);

            }
            else if (result.ResponseCode == HttpStatusCode.NotFound)
            {
                result.CheckEligibilityStatus = CheckEligibilityStatus.notEligible;

                _logger.LogInformation("Dwp is not found correlationId:{correlationId}", correlationId);
            }
            else
            {
                _logger.LogError($"Dwp Error unknown Response status code:-{result.ResponseCode}.");
                result.CheckEligibilityStatus = CheckEligibilityStatus.error;
            }

            return result;
        }

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