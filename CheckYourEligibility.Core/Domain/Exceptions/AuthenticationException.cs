namespace CheckYourEligibility.Core.Domain.Exceptions;

public class AuthenticationException : Exception
{
    public AuthenticationException(string errorCode, string errorDescription)
        : base(errorDescription)
    {
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
    }

    public string ErrorCode { get; }
    public string ErrorDescription { get; }
}

public class InvalidClientException : AuthenticationException
{
    public InvalidClientException(string message = "Invalid client credentials")
        : base("invalid_client", message)
    {
    }
}

public class InvalidScopeException : AuthenticationException
{
    public InvalidScopeException(
        string message = "The requested scope is invalid, unknown, or exceeds the scope granted by the resource owner")
        : base("invalid_scope", message)
    {
    }
}

public class ServerErrorException : AuthenticationException
{
    public ServerErrorException(string message = "The authorization server encountered an unexpected error")
        : base("server_error", message)
    {
    }
}

public class InvalidRequestException : AuthenticationException
{
    public InvalidRequestException(
        string message =
            "The request is missing a required parameter, includes an invalid parameter value, or is otherwise malformed")
        : base("invalid_request", message)
    {
    }
}