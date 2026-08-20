using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IGetUserRolesUseCase
{
    Task<UserRolesResponse> Execute(string userId);
}

public class GetUserRolesUseCase : IGetUserRolesUseCase
{
    private readonly IUsers _userGateway;

    public GetUserRolesUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    public async Task<UserRolesResponse> Execute(string userId)
    {
        var roles = await _userGateway.GetUserRoles(userId);

        return new UserRolesResponse
        {
            Data = roles.Select(role => new UserRoleItemResponse
            {
                UserRoleId = role.UserRoleId,
                UserId = role.UserId,
                RoleName = role.RoleName
            })
        };
    }
}
