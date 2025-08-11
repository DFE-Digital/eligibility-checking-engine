using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IRateLimit
{
    Task Create(RateLimitEvent item);
}