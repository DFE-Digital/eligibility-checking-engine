using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckEligibility
{
    Task<PostCheckResult> PostCheck<T>(T data) where T : IEligibilityServiceType;
    Task PostCheck<T>(T data, string groupId) where T : IEnumerable<IEligibilityServiceType>;

    Task<T> GetBulkCheckResults<T>(string guid) where T : IList<CheckEligibilityItem>;
    Task<T?> GetItem<T>(string guid, CheckEligibilityType type, bool isBatchRecord = false) where T : CheckEligibilityItem;

    Task<CheckEligibilityStatus?> GetStatus(string guid, CheckEligibilityType type);
    Task<BulkStatus?> GetBulkStatus(string guid);
    Task<IEnumerable<BulkCheck>?> GetBulkStatuses(string guid);

    Task<CheckEligibilityStatus?> ProcessCheck(string guid, AuditData? auditItem);
    Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid, EligibilityCheckStatusData data, CheckEligibilityType type);
    Task ProcessQueue(string queue);
}