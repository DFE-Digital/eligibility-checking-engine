using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain;
using BulkCheck = CheckYourEligibility.Core.Domain.BulkCheck;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IBulkCheck
{
    Task<string> CreateBulkCheck(BulkCheck bulkCheck);

    Task<IList<EligibilityCheck>> GetBulkCheckResults(string bulkCheckId);

    Task<BulkStatus?> GetBulkStatus(string guid);
    Task<IEnumerable<BulkCheck>?> GetBulkStatuses(string localAuthorityId, IList<int> allowedLocalAuthorityIds, string source, bool includeLast7DaysOnly = true);
    Task<BulkCheck?> GetBulkCheck(string guid);
	Task<IEnumerable<BulkCheck>?> GetBulkChecksByOrganisation(
		string organisationType,
        string source,
		int organisationId);
}