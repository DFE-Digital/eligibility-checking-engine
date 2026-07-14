using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface ICreateApplicationsFromBulkCheck
{
    Task<BulkCheck?> GetBulkCheck(string bulkCheckId);
    Task<List<EligibilityCheck>> GetEligibleChecks(string bulkCheckId);
    Task UpdateBulkCheckStatus(string bulkCheckId, BulkCheckStatus status);
}