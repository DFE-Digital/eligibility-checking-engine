using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IAddUserRoleUseCase
{
    Task<UserRole> Execute(string userId, UserRoleName roleName);
}

public class AddUserRoleUseCase : IAddUserRoleUseCase
{
    private readonly IUsers _userGateway;

    public AddUserRoleUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    public async Task<UserRole> Execute(string userId, UserRoleName roleName)
    {
        return await _userGateway.AddUserRole(userId, roleName);
    }
}

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
