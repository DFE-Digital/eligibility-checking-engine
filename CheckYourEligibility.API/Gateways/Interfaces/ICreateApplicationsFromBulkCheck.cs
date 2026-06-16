using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICreateApplicationsFromBulkCheck
{
    Task<BulkCheck?> GetBulkCheck(string bulkCheckId);
    Task<bool> HasEligibleChecks(string bulkCheckId);
    Task<List<EligibilityCheck>> GetEligibleChecks(string bulkCheckId);
    Task UpdateBulkCheckStatus(string bulkCheckId, BulkCheckStatus status);
}