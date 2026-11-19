using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Database;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface ILocalAuthority
{
    Task<LocalAuthority?> GetLocalAuthorityById(int localAuthorityId, EligibilityCheckContext? dbContextFactory);

    Task<LocalAuthority?> UpdateSchoolCanReviewEvidence(int localAuthorityId, bool value);
    Task<int> GetEligibilityPolicyIdForTypeAsync(int localAuthorityId, CheckEligibilityType type, EligibilityCheckContext? dbContextFactory);

    Task <List<EstablishmentResponseItem>> GetEstablishmentsByLocalAuthorityId(int localAuthorityId);

}
