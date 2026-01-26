using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckingEngine
{
    Task<CheckEligibilityStatus?> ProcessCheckAsync(string guid, AuditData? auditItem, IEligibilityCheckContext? dbContextFactory = null);
}