using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Helpers;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Diagnostics;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for processing eligibility checks
/// </summary>
public interface IProcessEligibilityCheckUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The ID of the eligibility check</param>
    /// <returns>Processed eligibility check status</returns>
    Task<CheckEligibilityStatusResponse> Execute(string guid, EligibilityCheckContext dbContextFactory = null);
}

public class ProcessEligibilityCheckUseCase : IProcessEligibilityCheckUseCase
{
    private readonly IConfiguration _configuration;
    private readonly IAudit _auditGateway;
    private readonly IHash _hashGateway;
    private readonly ICheckingEngine _checkingEngineGateway;
    private readonly ICheckEligibility _checkEligibilityGateway;
    private readonly ILogger<ProcessEligibilityCheckUseCase> _logger;
    private readonly string _UseEcsforChecks;
    private readonly string _UseEcsforChecksWF;

    public ProcessEligibilityCheckUseCase(
        IConfiguration configuration,
        ICheckingEngine checkingEngineGateway,
        IHash hashGateway,
        ICheckEligibility checkEligibilityGateway,
        IAudit auditGateway,
        ILogger<ProcessEligibilityCheckUseCase> logger)
    {
        _checkingEngineGateway = checkingEngineGateway;
        _configuration = configuration;
        _checkEligibilityGateway = checkEligibilityGateway;
        _auditGateway = auditGateway;
        _hashGateway = hashGateway;
        _logger = logger;
        _UseEcsforChecks = _configuration["Dwp:UseEcsforChecks"];
        _UseEcsforChecksWF = _configuration["Dwp:UseEcsforChecksWF"];
    }

    public async Task<CheckEligibilityStatusResponse> Execute(string guid, EligibilityCheckContext dbContextFactory = null)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

        try
        {
            var eligibilityCheck = await _checkEligibilityGateway.GetEligibilityCheckByIdAsync(guid, dbContextFactory);

            if (eligibilityCheck == null)
            {
                _logger.LogWarning(
                    $"Eligibility check with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
                throw new NotFoundException(guid);
            }

            var source = ProcessEligibilityCheckSource.HMRC;
            var checkStatusResult = CheckEligibilityStatus.parentNotFound;
            CAPIClaimResponse capiClaimResponse = new();

            var eceCheckResult = CheckEligibilityStatus.parentNotFound;
            var checkData = GetCheckProcessData(eligibilityCheck.Type, eligibilityCheck.CheckData);
            var auditItemTemplate = _auditGateway.AuditDataGet(AuditType.Check, string.Empty);
            string capiCorrelationId = Guid.NewGuid().ToString();

            if (_configuration.GetValue<string>("TestData:LastName") == checkData.LastName)
            {
                (checkStatusResult, eligibilityCheck.Tier) = TestDataHelper.TestDataCheck(
                    checkData.NationalInsuranceNumber,
                    checkData.NationalAsylumSeekerServiceNumber,
                    eligibilityCheck.Type,
                    _configuration);
                source = ProcessEligibilityCheckSource.TEST;
            }

            if (!string.IsNullOrEmpty(checkData.NationalInsuranceNumber))
            {
                string laId = EligibilityCheckHelper.ExtractLAIdFromScope(auditItemTemplate.scope);
                checkStatusResult = await _checkingEngineGateway.HMRC_Check(checkData, dbContextFactory);

                if (checkStatusResult == CheckEligibilityStatus.parentNotFound)
                {
                    if (_UseEcsforChecks == "true")
                    {
                        checkStatusResult = await _checkingEngineGateway.EcsCheck(checkData, laId);
                        source = ProcessEligibilityCheckSource.ECS;
                    }
                    else if (_UseEcsforChecks == "false")
                    {
                        capiClaimResponse = await _checkingEngineGateway.DwpCitizenCheck(checkData, checkStatusResult, capiCorrelationId);
                        checkStatusResult = capiClaimResponse.CheckEligibilityStatus;
                        source = ProcessEligibilityCheckSource.DWP;
                    }
                    else // do both checks
                    {
                        var ecsStatus = await _checkingEngineGateway.EcsCheck(checkData, laId);
                        capiClaimResponse = await _checkingEngineGateway.DwpCitizenCheck(checkData, ecsStatus, capiCorrelationId);
                        var eceStatus = capiClaimResponse.CheckEligibilityStatus;
                        source = ecsStatus != eceStatus ? ProcessEligibilityCheckSource.ECS_CONFLICT : ProcessEligibilityCheckSource.DWP;
                        checkStatusResult = ecsStatus;
                    }
                }
            }
            else if (!checkData.NationalAsylumSeekerServiceNumber.IsNullOrEmpty())
            {
                checkStatusResult = await _checkingEngineGateway.HO_Check(checkData, dbContextFactory);
                source = ProcessEligibilityCheckSource.HO;
            }

            eligibilityCheck.Status = checkStatusResult;
            eligibilityCheck.Updated = DateTime.UtcNow;

            if (checkStatusResult == CheckEligibilityStatus.error)
            {
                // Revert status back and do not save changes
                eligibilityCheck.Status = CheckEligibilityStatus.queuedForProcessing;
            }
            else
            {
                eligibilityCheck.EligibilityCheckHashID =
               await _hashGateway.Create(checkData, checkStatusResult, eligibilityCheck.Tier, source, auditItemTemplate, dbContextFactory);

                //If CAPI returns a different result from ECS
                // Create a record
                if (source == ProcessEligibilityCheckSource.ECS_CONFLICT)
                {
                    ECSConflict ecsConflictRecord = new()
                    {
                        CorrelationID = capiCorrelationId,
                        ECE_Status = eceCheckResult,
                        ECS_Status = checkStatusResult,
                        DateOfBirth = checkData.DateOfBirth,
                        LastName = checkData.LastName,
                        Nino = checkData.NationalInsuranceNumber,
                        Type = checkData.Type,
                        TimeStamp = DateTime.UtcNow,
                        EligibilityCheckHashID = eligibilityCheck.EligibilityCheckHashID,
                        CAPIEndpoint = capiClaimResponse.CAPIEndpoint,
                        Reason = capiClaimResponse.Reason,
                        CAPIResponseCode = capiClaimResponse.CAPIResponseCode,



                    };

                    await dbContextFactory.ECSConflicts.AddAsync(ecsConflictRecord);
                    await dbContextFactory.SaveChangesAsync();
                }

            }

            await _auditGateway.CreateAuditEntry(AuditType.Check, guid, dbContextFactory);

            _logger.LogInformation(
                $"Processed eligibility check with ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}, status: {checkStatusResult.ToString()}");

            var resultResponse = new CheckEligibilityStatusResponse
            {
                Data = new StatusValue
                {
                    Status = checkStatusResult.ToString(),
                    Tier = eligibilityCheck.Tier?.ToString()
                }
            };


            return resultResponse;
        }

        catch (ProcessCheckException ex)
        {
            _logger.LogError(ex,
                $"Error processing eligibility check with ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");
            throw new ValidationException(null, "Failed to process eligibility check.");
        }
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
    public async Task<CheckEligibilityStatusResponse> Execute_WorkingFamilies(string guid, EligibilityCheckContext dbContextFactory = null) {
        {
            if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

            try
            {
                var eligibilityCheck = await _checkEligibilityGateway.GetEligibilityCheckByIdAsync(guid, dbContextFactory);

                if (eligibilityCheck == null)
                {
                    _logger.LogWarning(
                        $"Eligibility check with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
                    throw new NotFoundException(guid);
                }

                WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
                var auditItemTemplate = _auditGateway.AuditDataGet(AuditType.Check, string.Empty);
                var source = ProcessEligibilityCheckSource.HMRC;
                string wfTestCodePrefix = _configuration.GetValue<string>("TestData:WFTestCodePrefix");
                var checkData = GetCheckProcessData(eligibilityCheck.Type, eligibilityCheck.CheckData);

                var sw = Stopwatch.StartNew();

                // Get event for TEST record
                if (!string.IsNullOrEmpty(wfTestCodePrefix) &&
                    checkData.EligibilityCode.StartsWith(wfTestCodePrefix))
                {
                    wfEvent = await TestDataHelper.Generate_Test_Working_Families_EventRecord(checkData, _configuration);
                    if (wfEvent == null) { eligibilityCheck.Status = CheckEligibilityStatus.notFound; }
                }
                // Get event for ECS record
                else if (_UseEcsforChecksWF == "true")
                {
                    await _checkingEngineGateway.ProcessWorkingFamiliesECSAsync(checkData, wfEvent, eligibilityCheck, source);

                    _logger.LogInformation($"Processing ECS WF check in {sw.ElapsedMilliseconds} ms");
                }
                // Get event for ECE record
                else
                {
                    wfEvent = await _checkingEngineGateway.ProcessWorkingFamiliesEventRecordAsync(checkData.DateOfBirth, checkData.EligibilityCode,
                        checkData.NationalInsuranceNumber, checkData.LastName);

                    if (wfEvent == null) { eligibilityCheck.Status = CheckEligibilityStatus.notFound; }

                    _logger.LogInformation($"Processing ECE WF check in {sw.ElapsedMilliseconds} ms");
                }

                var wfCheckData = JsonConvert.DeserializeObject<CheckProcessData>(eligibilityCheck.CheckData);

                // If event is returned initiate business logic.
                if (wfEvent != null && eligibilityCheck.Status != CheckEligibilityStatus.error && eligibilityCheck.Status != CheckEligibilityStatus.notFound)
                {

                    //Get current date and ensure it is between the DiscretionaryValidityStartDate and GracePeriodEndDate
                    var currentDate = DateTime.UtcNow.Date;

                    if (currentDate >= wfEvent.DiscretionaryValidityStartDate && currentDate <= wfEvent.GracePeriodEndDate)
                    {
                        eligibilityCheck.Status = CheckEligibilityStatus.eligible;
                    }
                    else
                    {
                        eligibilityCheck.Status = CheckEligibilityStatus.notEligible;
                    }

                }

                // Create hash just with the check request data to match on post requests
                eligibilityCheck.EligibilityCheckHashID =
                    await _hashGateway.Create(wfCheckData, eligibilityCheck.Status, eligibilityCheck.Tier, source, auditItemTemplate, dbContextFactory);

                // Now update the check data in the EligibilityCheckTable with all the neccessary fields
                // that needs to be returned on the GET request if a record has been found
                if (wfEvent != null && eligibilityCheck.Status != CheckEligibilityStatus.error && eligibilityCheck.Status != CheckEligibilityStatus.notFound)
                {

                    await _checkingEngineGateway.UpdateCheckDataWorkingFamiliesAsync(wfCheckData, wfEvent, eligibilityCheck, dbContextFactory);
                }
                _logger.LogInformation(
                    $"Processed eligibility check with ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}, status: {eligibilityCheck.Status.ToString()}");

                var resultResponse = new CheckEligibilityStatusResponse
                {
                    Data = new StatusValue
                    {
                        Status = eligibilityCheck.Status.ToString(),
                        Tier = eligibilityCheck.Tier?.ToString()
                    }
                };


                return resultResponse;
            }

            catch (ProcessCheckException ex)
            {
                _logger.LogError(ex,
                    $"Error processing eligibility check with ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");
                throw new ValidationException(null, "Failed to process eligibility check.");
            }
        }

    }
    private CheckProcessData GetCheckProcessData(CheckEligibilityType type, string data)
    {
        switch (type)
        {
            case CheckEligibilityType.FreeSchoolMeals:
            case CheckEligibilityType.TwoYearOffer:
            case CheckEligibilityType.EarlyYearPupilPremium:
                return EligibilityCheckHelper.MapCheckDataByType<CheckEligibilityRequestBulkData>(type, data);
            case CheckEligibilityType.WorkingFamilies:
                return EligibilityCheckHelper.MapCheckDataByType<CheckEligibilityRequestWorkingFamiliesBulkData>(type, data);
            default:
                throw new NotImplementedException($"Type:-{type} not supported.");
        }
    }

}