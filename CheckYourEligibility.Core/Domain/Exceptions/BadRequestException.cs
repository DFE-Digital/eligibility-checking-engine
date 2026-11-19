namespace CheckYourEligibility.Core.Domain.Exceptions;

public class BadRequest : Exception
{
    public BadRequest()
    {
    }

    public BadRequest(string message)
        : base(message)
    {
    }
}