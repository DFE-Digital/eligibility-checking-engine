using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

/// <summary>
///     Interface for updating eligibility check status
/// </summary>
public interface IUpdateEligibilityCheckStatusUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The ID of the eligibility check</param>
    /// <param name="model">The status update request</param>
    /// <returns>Updated eligibility check status</returns>
    Task<CheckEligibilityStatusResponse> Execute(string guid, EligibilityStatusUpdateRequest model);
}

public class UpdateEligibilityCheckStatusUseCase : IUpdateEligibilityCheckStatusUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<UpdateEligibilityCheckStatusUseCase> _logger;

    public UpdateEligibilityCheckStatusUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<UpdateEligibilityCheckStatusUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityStatusResponse> Execute(string guid, EligibilityStatusUpdateRequest model)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

        if (model == null || model.Data == null)
            throw new ValidationException(null, "Invalid Request, update data is required.");

        var response = await _checkGateway.UpdateEligibilityCheckStatus(guid, model.Data);
        if (response == null)
        {
            _logger.LogWarning(
                $"Failed to update eligibility check status for ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");
            throw new NotFoundException(guid);
        }

        _logger.LogInformation(
            $"Updated eligibility check status for ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

        return new CheckEligibilityStatusResponse
        {
            Data = response.Data
        };
    }
}