using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ILocalAuthority
{
    Task<LocalAuthority?> GetLocalAuthority(int localAuthorityId);
}
