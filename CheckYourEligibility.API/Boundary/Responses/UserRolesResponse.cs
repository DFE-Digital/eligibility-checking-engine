using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Responses;

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
