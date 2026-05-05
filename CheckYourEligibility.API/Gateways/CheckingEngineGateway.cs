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

    private readonly string isEligiblePrefix;
    private readonly string isInGracePeriodPrefix;
    private readonly string isNotYetEligiblePrefix;
    private readonly string isExpiredPrefix;

    public CheckingEngineGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext,
        IConfiguration configuration, IEcsAdapter ecsAdapter, IDwpAdapter dwpAdapter, IHash hashGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _ecsAdapter = ecsAdapter;
        _dwpAdapter = dwpAdapter;
        _hashGateway = hashGateway;
        _configuration = configuration;

        isEligiblePrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible");
        isInGracePeriodPrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:InGracePeriod");
        isNotYetEligiblePrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:NotYetEligible");
        isExpiredPrefix = _configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Expired");
    }

    /// <summary>
    /// Logic to find a match in Working families events' table
    /// Checks if record with the same EligibilityCode-ParentNINO-ChildDOB-ParentLastName exists in the WorkingFamiliesEvents Table
    /// </summary>
    /// <param name="checkData"></param>
    /// <returns></returns>
    public async Task<WorkingFamiliesEvent> ProcessWorkingFamiliesEventRecordAsync(string dateOfBirth,
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
    /// Make a call to ECS legacy to determine eligibility
    /// </summary>
    /// <param name="checkData"></param>
    /// <param name="wfEvent"></param>
    /// <param name="eligibilityCheck"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public async Task ProcessWorkingFamiliesECSAsync(CheckProcessData checkData, WorkingFamiliesEvent wfEvent, EligibilityCheck eligibilityCheck, ProcessEligibilityCheckSource source) {

        SoapCheckResponse innerResult = await _ecsAdapter.EcsWFCheck(checkData, eligibilityCheck.OrganisationID.ToString());

        eligibilityCheck.Status = convertEcsResultStatus(innerResult, CheckEligibilityType.WorkingFamilies);

        if (eligibilityCheck.Status != CheckEligibilityStatus.notFound && eligibilityCheck.Status != CheckEligibilityStatus.error)
        {
            wfEvent.EligibilityCode = checkData.EligibilityCode;
            wfEvent.ParentLastName = checkData.LastName;  //Return value as submitted in request
            wfEvent.DiscretionaryValidityStartDate = DateTime.Parse(innerResult.ValidityStartDate);
            wfEvent.ValidityEndDate = DateTime.Parse(innerResult.ValidityEndDate);
            wfEvent.GracePeriodEndDate = DateTime.Parse(innerResult.GracePeriodEndDate);
        }
        source = ProcessEligibilityCheckSource.ECS;

    }
    /// <summary>
    /// Update check data after check is processed
    /// with data expected in the response.
    /// </summary>
    /// <param name="checkdata"></param>
    /// <param name="wfEvent"></param>
    /// <param name="eligiblityCheck"></param>
    /// <param name="dbContextFactory">if not passed, singleton of dbContext is used</param>
    /// <returns></returns>
    public async Task UpdateCheckDataWorkingFamiliesAsync
        (CheckProcessData checkdata,
        WorkingFamiliesEvent wfEvent,
        EligibilityCheck eligiblityCheck,
        EligibilityCheckContext dbContextFactory = null) {

        var context = dbContextFactory ?? _db;
        checkdata.ValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
        checkdata.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
        checkdata.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
        checkdata.LastName = wfEvent.ParentLastName;
        checkdata.SubmissionDate = wfEvent.SubmissionDate.ToString("yyyy-MM-dd");

        eligiblityCheck.CheckData = JsonConvert.SerializeObject(checkdata);
        context.CheckEligibilities.Update(eligiblityCheck);
        eligiblityCheck.Updated = DateTime.UtcNow;
        await context.SaveChangesAsync();

    }

    //TODO: These two could be adapters
    public async Task<CheckEligibilityStatus> HO_Check(CheckProcessData data, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var checkReults = context.FreeSchoolMealsHO.Where(x =>
                x.NASS == data.NationalAsylumSeekerServiceNumber
                && x.DateOfBirth == DateTime.ParseExact(data.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None))
            .Select(x => x.LastName);

        return CheckSurname(data.LastName, checkReults);
    }

    public async Task<CheckEligibilityStatus> HMRC_Check(CheckProcessData data, EligibilityCheckContext dbContextFactory = null)
    {
        var context = dbContextFactory ?? _db;
        var checkReults = context.FreeSchoolMealsHMRC.Where(x =>
                x.FreeSchoolMealsHMRCID == data.NationalInsuranceNumber
                && x.DateOfBirth == DateTime.ParseExact(data.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None))
            .Select(x => x.Surname);

        return CheckSurname(data.LastName, checkReults);
    }


    public async Task<CheckEligibilityStatus> EcsCheck(CheckProcessData data, string LaId)
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

    #region Private
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