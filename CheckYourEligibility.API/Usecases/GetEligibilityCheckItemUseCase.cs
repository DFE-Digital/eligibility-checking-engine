using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving eligibility check item details
/// </summary>
public interface IGetEligibilityCheckItemUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The ID of the eligibility check</param>
    /// <returns>Eligibility check item details</returns>
    Task<CheckEligibilityItemResponse> Execute(string guid, CheckEligibilityType type = CheckEligibilityType.None);
}

public class GetEligibilityCheckItemUseCase : IGetEligibilityCheckItemUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<GetEligibilityCheckItemUseCase> _logger;

    public GetEligibilityCheckItemUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<GetEligibilityCheckItemUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityItemResponse> Execute(string guid, CheckEligibilityType type = CheckEligibilityType.None)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

        var response = await _checkGateway.GetItem<CheckEligibilityItem>(guid, type);
        if (response == null)
        {
            _logger.LogWarning(
                $"Eligibility check with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
            throw new NotFoundException(guid);
        }

        await _auditGateway.CreateAuditEntry(AuditType.Check, guid);

        _logger.LogInformation(
            $"Retrieved eligibility check details for ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

        return new CheckEligibilityItemResponse
        {
            Data = response,
            Links = new CheckEligibilityResponseLinks
            {
                Get_EligibilityCheck = $"{CheckLinks.GetLink}{guid}",
                Put_EligibilityCheckProcess = $"{CheckLinks.ProcessLink}{guid}",
                Get_EligibilityCheckStatus = $"{CheckLinks.GetLink}{guid}/Status"
            }
        };
    }
}