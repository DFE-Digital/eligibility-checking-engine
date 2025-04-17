using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving bulk upload results
/// </summary>
public interface IGetBulkUploadResultsUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The group ID of the bulk upload</param>
    /// <returns>Bulk upload results</returns>
    Task<CheckEligibilityBulkResponse> Execute(string guid);
}

public class GetBulkUploadResultsUseCase : IGetBulkUploadResultsUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<GetBulkUploadResultsUseCase> _logger;

    public GetBulkUploadResultsUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<GetBulkUploadResultsUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityBulkResponse> Execute(string guid)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, group ID is required.");

        var response = await _checkGateway.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid);
        if (response == null)
        {
            _logger.LogWarning(
                $"Bulk upload results with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
            throw new NotFoundException(guid);
        }

        await _auditGateway.CreateAuditEntry(AuditType.CheckBulkResults, guid);

        _logger.LogInformation(
            $"Retrieved bulk upload results for group ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

#pragma warning disable CS8601 // Possible null reference assignment.
        return new CheckEligibilityBulkResponse
        {
            Data = response as List<CheckEligibilityItem>
        };
#pragma warning restore CS8601 // Possible null reference assignment.
    }
}