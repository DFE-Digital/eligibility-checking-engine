using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways;
using CheckYourEligibility.Core.Database;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IHash
{
    Task<EligibilityCheckHash?> Exists(CheckProcessData item);

    Task<string> Create(CheckProcessData item, CheckEligibilityStatus checkResult, EligibilityTier? tier, ProcessEligibilityCheckSource source,
        EligibilityCheckContext dbContextFactory = null);
}