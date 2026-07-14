namespace CheckYourEligibility.Core.Domain.Exceptions;

public class ProcessCheckException : Exception
{
    public ProcessCheckException()
    {
    }

    public ProcessCheckException(string message)
        : base(message)
    {
    }
}