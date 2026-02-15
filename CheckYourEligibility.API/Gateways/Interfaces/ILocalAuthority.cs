using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ILocalAuthority
{
    Task<LocalAuthority?> GetLocalAuthorityById(int localAuthorityId);
}
