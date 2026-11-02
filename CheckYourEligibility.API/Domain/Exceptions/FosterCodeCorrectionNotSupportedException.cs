namespace CheckYourEligibility.API.Domain.Exceptions;

public class FosterCodeCorrectionNotSupportedException : Exception
{
    public FosterCodeCorrectionNotSupportedException()
        : base("Foster-family codes cannot be corrected through this service.")
    {
    }
}