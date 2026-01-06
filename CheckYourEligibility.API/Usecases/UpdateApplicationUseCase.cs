using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateApplicationUseCase
{
    Task<ApplicationUpdateResponse> Execute(string guid, ApplicationUpdateRequest model,
        List<int> allowedLocalAuthorityIds);
    Task<ApplicationUpdateResponse> ExecuteByReference(string reference, ApplicationUpdateRequest model,
        List<int> allowedLocalAuthorityIds);
}

public class UpdateApplicationUseCase : IUpdateApplicationUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    public UpdateApplicationUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }

    public async Task<ApplicationUpdateResponse> Execute(string guid, ApplicationUpdateRequest model,
        List<int> allowedLocalAuthorityIds)
    {
        // Get the local authority ID for the application
        var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForApplication(guid);

        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to create applications for this establishment's local authority");
        }

        var response = await _applicationGateway.UpdateApplication(guid, model.Data);
        if (response == null) return null;

        await _auditGateway.CreateAuditEntry(AuditType.Application, guid);

        return new ApplicationUpdateResponse
        {
            Data = response.Data
        };
    }

    public async Task<ApplicationUpdateResponse> ExecuteByReference(string reference, ApplicationUpdateRequest model,
        List<int> allowedLocalAuthorityIds)
    {
        // Get the local authority ID for the application
        var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForApplicationByReference(reference);

        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to create applications for this establishment's local authority");
        }

        var response = await _applicationGateway.UpdateApplicationByReference(reference, model.Data);
        if (response == null) return null;

        
        await _auditGateway.CreateAuditEntry(AuditType.Application, reference);

        return new ApplicationUpdateResponse
        {
            Data = response.Data
        };
    }
}
