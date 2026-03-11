using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IDeleteWorkingFamiliesEventUseCase
{
    Task<bool> Execute(string hmrcId);
}

public class DeleteWorkingFamiliesEventUseCase : IDeleteWorkingFamiliesEventUseCase
{
    private readonly IWorkingFamiliesEvent _gateway;
    private readonly IAudit _auditGateway;
    private readonly ILogger<DeleteWorkingFamiliesEventUseCase> _logger;

    public DeleteWorkingFamiliesEventUseCase(
        IWorkingFamiliesEvent gateway,
        IAudit auditGateway,
        ILogger<DeleteWorkingFamiliesEventUseCase> logger)
    {
        _gateway = gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<bool> Execute(string hmrcId)
    {
        if (string.IsNullOrWhiteSpace(hmrcId))
            throw new ArgumentNullException(nameof(hmrcId), "HMRC eligibility event id must not be empty");

        var deleted = await _gateway.DeleteWorkingFamiliesEvent(hmrcId);

        if (deleted)
        {
            await _auditGateway.CreateAuditEntry(AuditType.WorkingFamilies, hmrcId);
            _logger.LogInformation("Working families event soft-deleted for HMRC id {HMRCId}", hmrcId);
        }

        return deleted;
    }
}
