using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

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
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);
    }
}