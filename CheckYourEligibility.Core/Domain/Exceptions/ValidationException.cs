using CheckYourEligibility.Core.Boundary.Responses;

namespace CheckYourEligibility.Core.Domain.Exceptions;

public class ValidationException : Exception
{
    public List<Error> Errors;

    public ValidationException(List<Error> errors, string errorDescription)
        : base(errorDescription)
    {
        if (errors == null) errors = new List<Error> { new() { Title = errorDescription } };
        Errors = errors;
    }
}