using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface ICleanUpEligibilityChecksUseCase
{
    Task Execute();
}

public class CleanUpEligibilityChecksUseCase : ICleanUpEligibilityChecksUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;

    public CleanUpEligibilityChecksUseCase(IAdministration Gateway, IAudit auditGateway)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
    }

    public async Task Execute()
    {
        await _gateway.CleanUpEligibilityChecks();
    }
}