using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckEligibility
{
    Task<PostCheckResult> PostCheck<T>(T data) where T : IEligibilityServiceType;
    Task PostCheck<T>(T data, string groupId) where T : IEnumerable<IEligibilityServiceType>;
    Task<string> CreateBulkCheck(Domain.BulkCheck bulkCheck);

    Task<T> GetBulkCheckResults<T>(string guid) where T : IList<CheckEligibilityItem>;
    Task<T?> GetItem<T>(string guid) where T : CheckEligibilityItem;

    Task<CheckEligibilityStatus?> GetStatus(string guid);
    Task<BulkStatus?> GetBulkStatus(string guid);
    Task<IEnumerable<Domain.BulkCheck>?> GetBulkStatuses(string guid);

    Task<CheckEligibilityStatus?> ProcessCheck(string guid, AuditData? auditItem);
    Task<CheckEligibilityStatusResponse> UpdateEligibilityCheckStatus(string guid, EligibilityCheckStatusData data);
    Task ProcessQueue(string queue);
    Task<CheckEligibilityBulkDeleteResponse> DeleteByGroup(string groupId);
}