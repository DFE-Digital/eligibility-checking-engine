// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IHash
{
    Task<EligibilityCheckHash?> Exists(CheckProcessData item);

    Task<CheckProcessData?> GetCachedCheckDataAsync(CheckProcessData checkData, EligibilityCheckHash hashResult);

    Task<string> Create(CheckProcessData item, CheckEligibilityStatus checkResult, EligibilityTier? tier, ProcessEligibilityCheckSource source,
        EligibilityCheckContext dbContextFactory = null);
}