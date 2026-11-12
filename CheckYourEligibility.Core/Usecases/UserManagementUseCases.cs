using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface ISearchUsersUseCase
{
    Task<UsersSearchResponse> Execute(string? search, UserType? userType, int pageNumber, int pageSize);
}

public class SearchUsersUseCase : ISearchUsersUseCase
{
    private readonly IUsers _userGateway;

    public SearchUsersUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    public async Task<UsersSearchResponse> Execute(string? search, UserType? userType, int pageNumber, int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var (users, total) = await _userGateway.SearchUsers(search, userType, pageNumber, pageSize);

        return new UsersSearchResponse
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalNumberOfRecords = total,
            Data = users
        };
    }
}

public interface IGetUserUseCase
{
    Task<UserDetailsResponse> Execute(string userId);
}

public class GetUserUseCase : IGetUserUseCase
{
    private readonly IUsers _userGateway;

    public GetUserUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    public async Task<UserDetailsResponse> Execute(string userId)
    {
        var (user, roles) = await _userGateway.GetUser(userId);

        return new UserDetailsResponse
        {
            Data = user,
            Roles = roles.Select(role => new UserRoleItemResponse
            {
                UserRoleId = role.UserRoleId,
                UserId = role.UserId,
                RoleName = role.RoleName
            })
        };
    }
}

public interface IUpdateUserEnabledUseCase
{
    Task Execute(string userId, bool isEnabled);
}

public class UpdateUserEnabledUseCase : IUpdateUserEnabledUseCase
{
    private readonly IUsers _userGateway;

    public UpdateUserEnabledUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    public Task Execute(string userId, bool isEnabled)
    {
        return _userGateway.UpdateUserEnabled(userId, isEnabled);
    }
}
