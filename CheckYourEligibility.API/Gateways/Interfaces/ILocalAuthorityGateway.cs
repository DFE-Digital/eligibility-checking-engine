using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ILocalAuthorityGateway
{
    Task<LocalAuthority?> GetById(int localAuthorityId);
}
