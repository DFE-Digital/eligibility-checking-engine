using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class UserCreateRequest
{
    public UserData? Data { get; set; }
    public CheckMetaData? metaData { get; set; }
}

public class UserData
{
    public string Email { get; set; }
    public string Reference { get; set; }
}