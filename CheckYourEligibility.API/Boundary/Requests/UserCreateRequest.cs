// Ignore Spelling: Fsm

namespace CheckYourEligibility.API.Boundary.Requests;

public class UserCreateRequest
{
    public UserData? Data { get; set; }
}

public class UserData
{
    public string Email { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}