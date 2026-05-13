using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ILocalAuthority
{
    Task<LocalAuthority?> GetLocalAuthorityById(int localAuthorityId);

    Task<LocalAuthority?> UpdateSchoolCanReviewEvidence(int localAuthorityId, bool value);
    Task<int?> GetEligibilityPolicyIdForTypeAsync(int localAuthorityId, CheckEligibilityType type);

}
