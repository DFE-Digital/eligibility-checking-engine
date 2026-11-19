using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface ICheckEligibility
{
    Task<PostCheckResult> PostCheck<T>(T data, CheckMetaData meta) where T : IEligibilityServiceType;
    Task PostCheck<T>(T data, string bulkCheckId, CheckMetaData meta) where T : IEnumerable<IEligibilityServiceType>;
    Task<EligibilityCheck> GetItem(string guid);

    Task<(CheckEligibilityStatus?, EligibilityTier?, string?)> GetStatusAsync(
        string guid,
        CheckEligibilityType type);

    Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid, EligibilityCheckStatusData data, EligibilityCheckContext dbContextFactory = null);
    Task<CheckEligibilityBulkDeleteResponseData> DeleteByBulkCheckId(string bulkCheckId);

}