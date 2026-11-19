using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

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
