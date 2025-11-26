// Ignore Spelling: Fsm

namespace CheckYourEligibility.API.Domain.Exceptions;

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