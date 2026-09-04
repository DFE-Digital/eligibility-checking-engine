using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IRemoveUserRoleUseCase
{
    Task<bool> Execute(string userId, UserRoleName roleName);
}

public class RemoveUserRoleUseCase : IRemoveUserRoleUseCase
{
    private readonly IUsers _userGateway;

    public RemoveUserRoleUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    public async Task<bool> Execute(string userId, UserRoleName roleName)
    {
        return await _userGateway.RemoveUserRole(userId, roleName);
    }
}
