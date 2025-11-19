using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IBulkCheck
{
    Task<string> CreateBulkCheck(BulkCheck bulkCheck);

    Task<T> GetBulkCheckResults<T>(string guid) where T : IList<CheckEligibilityItem>;

    Task<BulkStatus?> GetBulkStatus(string guid);
    Task<IEnumerable<BulkCheck>?> GetBulkStatuses(string localAuthorityId, IList<int> allowedLocalAuthorityIds, bool includeLast7DaysOnly = true);
    Task<BulkCheck?> GetBulkCheck(string guid);
}