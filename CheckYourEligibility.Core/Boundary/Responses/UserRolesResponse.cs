
using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class UserRolesResponse
{
    public IEnumerable<UserRoleItemResponse> Data { get; set; } = [];
}

public class UserRoleItemResponse
{
    public Guid UserRoleId { get; set; }

    public string UserId { get; set; }

    public UserRoleName RoleName { get; set; }
}
