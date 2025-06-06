using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateApplicationStatusUseCase
{
    Task<ApplicationStatusUpdateResponse> Execute(string guid, ApplicationStatusUpdateRequest model, List<int> allowedLocalAuthorityIds);
}

public class UpdateApplicationStatusUseCase : IUpdateApplicationStatusUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    public UpdateApplicationStatusUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }

    public async Task<ApplicationStatusUpdateResponse> Execute(string guid, ApplicationStatusUpdateRequest model, List<int> allowedLocalAuthorityIds)
    {
        // Get the local authority ID for the application
        var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForApplication(guid);

        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException("You do not have permission to create applications for this establishment's local authority");
        }

        var response = await _applicationGateway.UpdateApplicationStatus(guid, model.Data);
        if (response == null) return null;

        await _auditGateway.CreateAuditEntry(AuditType.Application, guid);

        return new ApplicationStatusUpdateResponse
        {
            Data = response.Data
        };
    }
}