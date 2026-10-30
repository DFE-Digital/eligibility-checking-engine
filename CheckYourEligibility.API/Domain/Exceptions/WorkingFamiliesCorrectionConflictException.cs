namespace CheckYourEligibility.API.Domain.Exceptions;

public class WorkingFamiliesCorrectionConflictException : Exception
{
    public WorkingFamiliesCorrectionConflictException()
        : base(
            "An error occurred - a record update conflict was detected - please try again")
    {
    }
}