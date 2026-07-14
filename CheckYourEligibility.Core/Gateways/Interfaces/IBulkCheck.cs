using CheckYourEligibility.Core.Boundary.Responses;
using BulkCheck = CheckYourEligibility.Core.Domain.BulkCheck;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IBulkCheck
{
    Task<string> CreateBulkCheck(BulkCheck bulkCheck);

    Task<T> GetBulkCheckResults<T>(string guid) where T : IList<CheckEligibilityItem>;

    Task<BulkStatus?> GetBulkStatus(string guid);
    Task<IEnumerable<BulkCheck>?> GetBulkStatuses(string localAuthorityId, IList<int> allowedLocalAuthorityIds, string source, bool includeLast7DaysOnly = true);
    Task<BulkCheck?> GetBulkCheck(string guid);
	Task<IEnumerable<BulkCheck>?> GetBulkChecksByOrganisation(
		string organisationType,
        string source,
		int organisationId);
}