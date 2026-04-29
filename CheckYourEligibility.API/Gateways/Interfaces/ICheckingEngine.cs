using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckingEngine
{
    Task<(CheckEligibilityStatus?, EligibilityTier?)> ProcessCheckAsync(string guid, AuditData? auditItem, EligibilityCheckContext dbContextFactory = null);
}