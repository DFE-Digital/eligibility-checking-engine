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
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs for the user (0 means admin access to all)</param>
    /// <returns>Bulk upload results</returns>
    Task<CheckEligibilityBulkResponse> Execute(string guid, IList<int> allowedLocalAuthorityIds);
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

    /// <summary>
    ///     Execute the use case to retrieve bulk upload results with local authority validation
    /// </summary>
    /// <param name="guid">The group ID of the bulk upload</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs for the user (0 means admin access to all)</param>
    /// <returns>Bulk upload results</returns>
    public async Task<CheckEligibilityBulkResponse> Execute(string guid, IList<int> allowedLocalAuthorityIds)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(new List<Boundary.Responses.Error>(), "Invalid Request, group ID is required.");

        // First, verify the bulk check exists and user has permission to access it
        var bulkCheck = await _checkGateway.GetBulkCheck(guid);
        if (bulkCheck == null)
        {
            _logger.LogWarning(
                $"Bulk upload with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
            throw new NotFoundException(guid);
        }

        // Validate local authority access
        if (!allowedLocalAuthorityIds.Contains(0) && (bulkCheck.LocalAuthorityId == null || !allowedLocalAuthorityIds.Contains(bulkCheck.LocalAuthorityId.Value)))
        {
            _logger.LogWarning(
                $"User attempted to access bulk upload {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} belonging to local authority {bulkCheck.LocalAuthorityId} without permission");
            throw new UnauthorizedAccessException($"You do not have permission to access bulk check {guid}");
        }

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

        return new CheckEligibilityBulkResponse
        {
            Data = response as List<CheckEligibilityItem> ?? new List<CheckEligibilityItem>()
        };
    }
}