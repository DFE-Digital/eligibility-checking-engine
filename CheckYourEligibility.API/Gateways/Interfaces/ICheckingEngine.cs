using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckingEngine
{
    Task<CheckEligibilityStatus> HMRC_Check(CheckProcessData data, EligibilityCheckContext dbContextFactory = null);
    Task<CheckEligibilityStatus> HO_Check(CheckProcessData data, EligibilityCheckContext dbContextFactory = null);
    Task<CheckEligibilityStatus> EcsCheck(CheckProcessData data, string LaId);
    Task<CAPIClaimResponse> DwpCitizenCheck(CheckProcessData data, CheckEligibilityStatus checkResult, string correlationId);
    Task ProcessWorkingFamiliesECSAsync(CheckProcessData checkData, WorkingFamiliesEvent wfEvent, EligibilityCheck eligibilityCheck, ProcessEligibilityCheckSource source);

    Task UpdateCheckDataWorkingFamiliesAsync
        (CheckProcessData checkdata,
        WorkingFamiliesEvent wfEvent,
        EligibilityCheck eligiblityCheck,
        EligibilityCheckContext dbContextFactory = null);
    Task<WorkingFamiliesEvent> ProcessWorkingFamiliesEventRecordAsync(string dateOfBirth,
        string eligibilityCode, string nino, string lastName, EligibilityCheckContext dbContextFactory = null);
}
