using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckEligibility
{
    Task<PostCheckResult> PostCheck<T>(T data) where T : IEligibilityServiceType;
    Task PostCheck<T>(T data, string bulkCheckId) where T : IEnumerable<IEligibilityServiceType>;
    Task<T?> GetItem<T>(string guid, CheckEligibilityType type, bool isBatchRecord = false)
        where T : CheckEligibilityItem;

    Task<CheckEligibilityStatus?> GetStatus(string guid, CheckEligibilityType type);

    Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid, EligibilityCheckStatusData data);
    Task<CheckEligibilityBulkDeleteResponseData> DeleteByBulkCheckId(string bulkCheckId);
}