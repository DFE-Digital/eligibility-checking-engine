// Ignore Spelling: Fsm

namespace CheckYourEligibility.API.Boundary.Requests;

public class UserCreateRequest
{
    public UserData? Data { get; set; }
    public CheckMetaData? MetaData { get; set; }
}

public class UserData 
{
    public string Email { get; set; }
    public string Reference { get; set; }
}