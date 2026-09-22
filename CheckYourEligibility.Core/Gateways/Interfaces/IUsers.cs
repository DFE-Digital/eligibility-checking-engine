using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IUsers
{
    Task<string> CreateOrUpdateFSMParentUser(UserCreateRequest request);

    Task<string> CreateOrUpdateUser(UserCreateRequest request);

    Task<IList<UserRole>> GetUserRoles(string userId);

    Task<UserRole> AddUserRole(string userId, UserRoleName roleName);

    Task<bool> RemoveUserRole(string userId, UserRoleName roleName);
}