namespace CheckYourEligibility.API.Domain.Exceptions;

public class WorkingFamiliesCorrectionPartnerMissingException : Exception
{
    public WorkingFamiliesCorrectionPartnerMissingException()
        : base("This record does not have a partner to amend.")
    {
    }
}