using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Database;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface ICheckingEngine
{
    Task<(CheckEligibilityStatus?, EligibilityTier?)> ProcessCheckAsync(string guid, EligibilityCheckContext dbContextFactory = null);
}