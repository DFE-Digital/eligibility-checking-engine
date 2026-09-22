using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

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
