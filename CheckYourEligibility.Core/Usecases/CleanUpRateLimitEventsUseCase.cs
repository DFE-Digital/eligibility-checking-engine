using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface ICleanUpRateLimitEventsUseCase
{
    Task Execute();
}

public class CleanUpRateLimitEventsUseCase : ICleanUpRateLimitEventsUseCase
{
    private readonly IRateLimit _gateway;

    public CleanUpRateLimitEventsUseCase(IRateLimit Gateway)
    {
        _gateway = Gateway;
    }

    public async Task Execute()
    {
        await _gateway.CleanUpRateLimitEvents();
    }
}