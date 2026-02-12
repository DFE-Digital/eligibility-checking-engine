// Ignore Spelling: FSM

namespace CheckYourEligibility.API.Domain.Constants;

public static class CheckLinks
{
    public const string GetLink = "/check/";
    public const string ProcessLink = "/engine/process/";
    public const string Status = "/status/";

    public const string BulkCheckLink = "/bulk-check/";
    public const string BulkCheckProgress = "/progress";
    public const string BulkCheckResults = "/";
}

public static class ApplicationLinks
{
    public const string GetLinkApplication = "/application/";
}