using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IGetApplicationUseCase
{
    Task<ApplicationItemResponse> Execute(string guid, List<int> allowedLocalAuthorityIds);
}

public class GetApplicationUseCase : IGetApplicationUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    public GetApplicationUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }    /// <summary>
    /// Gets an application by guid after validating local authority permissions
    /// </summary>
    /// <param name="guid">The application guid</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The application response</returns>
    public async Task<ApplicationItemResponse> Execute(string guid, List<int> allowedLocalAuthorityIds)
    {
        // Get the local authority ID for the application
        var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForApplication(guid);

        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException("You do not have permission to access applications for this establishment's local authority");
        }

        var response = await _applicationGateway.GetApplication(guid);
        if (response == null) return null!;
        await _auditGateway.CreateAuditEntry(AuditType.Application, guid);

        return new ApplicationItemResponse
        {
            Data = response,
            Links = new ApplicationResponseLinks
            {
                get_Application = $"{ApplicationLinks.GetLinkApplication}{response.Id}"
            }
        };
    }
}