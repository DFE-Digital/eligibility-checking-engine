using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface ICleanUpRateLimitEventsUseCase
{
    Task Execute();
}

public class CleanUpRateLimitEventsUseCase : ICleanUpRateLimitEventsUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IRateLimit _gateway;

    public CleanUpRateLimitEventsUseCase(IRateLimit Gateway, IAudit auditGateway)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
    }

    public async Task Execute()
    {
        await _gateway.CleanUpRateLimitEvents();
    }
}