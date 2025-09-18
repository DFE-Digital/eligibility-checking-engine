using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IRateLimit
{
    Task Create(RateLimitEvent item);
    Task UpdateStatus(string guid, bool accepted);
    Task<int> GetQueriesInWindow(string partition, DateTime eventTimeStamp, TimeSpan windowLength);
    Task CleanUpRateLimitEvents();
}