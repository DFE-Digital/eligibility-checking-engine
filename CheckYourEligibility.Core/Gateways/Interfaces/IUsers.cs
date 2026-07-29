using CheckYourEligibility.Core.Boundary.Requests;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IUsers
{
    Task<string> CreateOrUpdateFSMParentUser(UserCreateRequest request);

    Task CreateOrUpdateUser(UserCreateRequest request);
}