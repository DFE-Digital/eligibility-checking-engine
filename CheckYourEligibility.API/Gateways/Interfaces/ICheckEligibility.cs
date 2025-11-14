using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckEligibility
{
    Task<PostCheckResult> PostCheck<T>(T data) where T : IEligibilityServiceType;
    Task PostCheck<T>(T data, string bulkCheckId) where T : IEnumerable<IEligibilityServiceType>;
    Task<string> CreateBulkCheck(BulkCheck bulkCheck);

    Task<T> GetBulkCheckResults<T>(string guid) where T : IList<CheckEligibilityItem>;

    Task<T?> GetItem<T>(string guid, CheckEligibilityType type, bool isBatchRecord = false)
        where T : CheckEligibilityItem;

    Task<CheckEligibilityStatus?> GetStatus(string guid, CheckEligibilityType type);
    Task<BulkStatus?> GetBulkStatus(string guid);
    Task<IEnumerable<BulkCheck>?> GetBulkStatuses(string localAuthorityId, IList<int> allowedLocalAuthorityIds, bool includeLast7DaysOnly = true);
    Task<BulkCheck?> GetBulkCheck(string guid);

    Task<CheckEligibilityStatus?> ProcessCheck(string guid, AuditData? auditItem);
    Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid, EligibilityCheckStatusData data);
    Task ProcessQueue(string queue);
    Task<CheckEligibilityBulkDeleteResponseData> DeleteByBulkCheckId(string bulkCheckId);
}