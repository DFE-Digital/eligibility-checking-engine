using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for the restore archived application status use case
/// </summary>
public interface IRestoreArchivedApplicationStatusUseCase
{
    /// <summary>
    /// Restores the archived status of an application
    /// </summary>
    /// <param name="guid">The ID of the application to restore</param>
    /// <param name="allowedLocalAuthorityIds"></param>
    Task<ApplicationStatusRestoreResponse> Execute(string guid, List<int> allowedLocalAuthorityIds);
}

public class RestoreArchivedApplicationStatusUseCase : IRestoreArchivedApplicationStatusUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    public RestoreArchivedApplicationStatusUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }

    public async Task<ApplicationStatusRestoreResponse> Execute(string guid, List<int> allowedLocalAuthorityIds)
    {
       
       var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForApplication(guid);
        
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to update applications for this establishment's local authority");
        }

        var response = await _applicationGateway.RestoreArchivedApplicationStatus(guid);
        if (response == null) return null;

        await _auditGateway.CreateAuditEntry(AuditType.Application, guid);

        return new ApplicationStatusRestoreResponse
        {
            Data = response.Data
        };
    }

}