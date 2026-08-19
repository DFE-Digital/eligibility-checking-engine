using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways.Interfaces;

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