namespace CheckYourEligibility.API.Domain.Exceptions;

/// <summary>
/// The stored records do not identify a safe correction target.
/// </summary>
public class WorkingFamiliesCorrectionDataException : Exception
{
    public WorkingFamiliesCorrectionDataException(string message)
        : base(message)
    {
    }
}