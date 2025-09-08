using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for the delete application use case
/// </summary>
public interface IDeleteApplicationUseCase
{
    /// <summary>
    /// Deletes an application after validating local authority permissions
    /// </summary>
    /// <param name="guid">The application GUID to delete</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>Task</returns>
    Task Execute(string guid, List<int> allowedLocalAuthorityIds);
}

/// <summary>
/// Implementation of the delete application use case
/// </summary>
public class DeleteApplicationUseCase : IDeleteApplicationUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    /// <summary>
    /// Constructor for DeleteApplicationUseCase
    /// </summary>
    /// <param name="applicationGateway">The application gateway</param>
    /// <param name="auditGateway">The audit gateway</param>
    public DeleteApplicationUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }

    /// <summary>
    /// Deletes an application after validating local authority permissions
    /// </summary>
    /// <param name="guid">The application GUID to delete</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>Task</returns>
    public async Task Execute(string guid, List<int> allowedLocalAuthorityIds)
    {
        // Validate parameters
        if (allowedLocalAuthorityIds == null)
        {
            throw new ArgumentNullException(nameof(allowedLocalAuthorityIds));
        }

        // First check if the application exists and get its local authority ID
        var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForApplication(guid);

        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete applications for this establishment's local authority");
        }

        // Delete the application
        var deleted = await _applicationGateway.DeleteApplication(guid);
        
        if (!deleted)
        {
            throw new NotFoundException($"Application with ID {guid} not found");
        }

        // Create audit entry
        await _auditGateway.CreateAuditEntry(AuditType.Application, guid);
    }
}
