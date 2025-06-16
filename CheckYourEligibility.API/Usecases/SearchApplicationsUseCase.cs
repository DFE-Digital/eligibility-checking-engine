using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using System;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for the search applications use case
/// </summary>
public interface ISearchApplicationsUseCase
{
    /// <summary>
    /// Searches for applications after validating local authority permissions
    /// </summary>
    /// <param name="model">The application search request data</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The search results response</returns>
    Task<ApplicationSearchResponse> Execute(ApplicationRequestSearch model, List<int> allowedLocalAuthorityIds);
}

/// <summary>
/// Implementation of the search applications use case
/// </summary>
public class SearchApplicationsUseCase : ISearchApplicationsUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    /// <summary>
    /// Constructor for SearchApplicationsUseCase
    /// </summary>
    /// <param name="applicationGateway">The application gateway</param>
    /// <param name="auditGateway">The audit gateway</param>
    public SearchApplicationsUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }    /// <summary>
         /// Searches for applications after validating local authority permissions
         /// </summary>
         /// <param name="model">The application search request data</param>
         /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
         /// <returns>The search results response</returns>
    public async Task<ApplicationSearchResponse> Execute(ApplicationRequestSearch model, List<int> allowedLocalAuthorityIds)
    {
        if (model?.Data == null)
        {
            throw new ArgumentException("Invalid request, data is required");
        }

        // Either LocalAuthority or Establishment or both must be provided
        if (model.Data.LocalAuthority == null && model.Data.Establishment == null)
        {
            throw new ArgumentException("Either LocalAuthority or Establishment must be specified");
        }

        // Validate LocalAuthority if provided
        if (model.Data.LocalAuthority != null)
        {
            // If not 'all', must match one of the allowed LocalAuthorities
            if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(model.Data.LocalAuthority.Value))
            {
                throw new UnauthorizedAccessException("You do not have permission to search applications for this local authority");
            }
        }

        // Validate Establishment if provided
        if (model.Data.Establishment != null)
        {
            // Get the local authority ID for the establishment and check permissions
            var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForEstablishment(model.Data.Establishment.Value);

            // If not 'all', must match one of the allowed LocalAuthorities
            if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
            {
                throw new UnauthorizedAccessException("You do not have permission to search applications for this establishment's local authority");
            }
        }
        var response = await _applicationGateway.GetApplications(model);

        if (response == null || !response.Data.Any()) return new ApplicationSearchResponse { Data = [], TotalPages = 0, TotalRecords = 0 };
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);

        return response;
    }
}