using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface IDeleteWorkingFamiliesEventUseCase
{
    Task<bool> Execute(string hmrcId);
}

public class DeleteWorkingFamiliesEventUseCase : IDeleteWorkingFamiliesEventUseCase
{
    private readonly IWorkingFamiliesEvent _gateway;
    private readonly ILogger<DeleteWorkingFamiliesEventUseCase> _logger;

    public DeleteWorkingFamiliesEventUseCase(
        IWorkingFamiliesEvent gateway,
        ILogger<DeleteWorkingFamiliesEventUseCase> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<bool> Execute(string hmrcId)
    {
        if (string.IsNullOrWhiteSpace(hmrcId))
            throw new ArgumentNullException(nameof(hmrcId), "HMRC eligibility event id must not be empty");

        var deleted = await _gateway.DeleteWorkingFamiliesEvent(hmrcId);

        if (deleted)
        {
            var safeId = hmrcId?.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogInformation("Working families event soft-deleted for HMRC id {HMRCId}", safeId);
        }

        return deleted;
    }
}
