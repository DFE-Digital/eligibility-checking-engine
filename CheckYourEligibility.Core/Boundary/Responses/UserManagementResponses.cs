using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class UsersSearchResponse
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalNumberOfRecords { get; set; }
    public IEnumerable<User> Data { get; set; } = [];
}

public class UserDetailsResponse
{
    public User Data { get; set; }
    public IEnumerable<UserRoleItemResponse> Roles { get; set; } = [];
}

public class UserPatchRequest
{
    public UserPatchData Data { get; set; }
}

public class UserPatchData
{
    public bool? IsEnabled { get; set; }
}
