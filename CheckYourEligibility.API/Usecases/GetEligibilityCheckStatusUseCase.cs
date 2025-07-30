using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving eligibility check status
/// </summary>
public interface IGetEligibilityCheckStatusUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The ID of the eligibility check</param>
    /// <param name="type">The type of the eligibility check that is being queried</param>
    /// <returns>Eligibility check status</returns>
    Task<CheckEligibilityStatusResponse> Execute(string guid, CheckEligibilityType type = CheckEligibilityType.None);
}

public class GetEligibilityCheckStatusUseCase : IGetEligibilityCheckStatusUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<GetEligibilityCheckStatusUseCase> _logger;

    public GetEligibilityCheckStatusUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<GetEligibilityCheckStatusUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityStatusResponse> Execute(string guid, CheckEligibilityType type = CheckEligibilityType.None)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

        var response = await _checkGateway.GetStatus(guid, type);
        if (response == null)
        {
            _logger.LogWarning(
                $"Eligibility check with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
            throw new NotFoundException(guid);
        }

        await _auditGateway.CreateAuditEntry(AuditType.Check, guid);

        _logger.LogInformation(
            $"Retrieved eligibility check status for ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

        return new CheckEligibilityStatusResponse
        {
            Data = new StatusValue
            {
                Status = response.Value.ToString()
            }
        };
    }
}