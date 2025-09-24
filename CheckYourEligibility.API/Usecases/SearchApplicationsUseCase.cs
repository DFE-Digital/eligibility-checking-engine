using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Extensions;
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
    /// <param name="allowedMultiAcademyTrustIds">List of allowed multi academy trust IDs from user claims</param>
    /// <returns>The search results response</returns>
    Task<ApplicationSearchResponse> Execute(ApplicationRequestSearch model, List<int> allowedLocalAuthorityIds, List<int> allowedMultiAcademyTrustIds);
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
    }

    /// <summary>
    /// Searches for applications after validating local authority permissions
    /// </summary>
    /// <param name="model">The application search request data</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The search results response</returns>
    public async Task<ApplicationSearchResponse> Execute(ApplicationRequestSearch model,
        List<int> allowedLocalAuthorityIds, List<int> allowedMultiAcademyTrustIds)
    {
        if (model?.Data == null)
        {
            throw new ArgumentException("Invalid request, data is required");
        }

        // Either LocalAuthority, Establishment, or MultiAcademyTrust or combination must be provided
        if (model.Data.LocalAuthority == null && model.Data.Establishment == null && model.Data.MultiAcademyTrust == null)
        {
            throw new ArgumentException("Either LocalAuthority, Establishment, or MultiAcademyTrust must be specified");
        }

        //TODO: Check whether this is a valid thing to be checking
        if (model.Data.LocalAuthority != null && model.Data.MultiAcademyTrust != null)
        {
            throw new ArgumentException("Can not search for both LocalAuthority and MultiAcademyTrust");
        }

        validateLocalAuthority(model.Data.LocalAuthority, allowedLocalAuthorityIds);
        await validateEstablishment(model.Data.Establishment, allowedLocalAuthorityIds);

        var response = await _applicationGateway.GetApplications(model);

        if (response == null || !response.Data.Any())
            return new ApplicationSearchResponse { Data = [], TotalPages = 0, TotalRecords = 0 };
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);

        return response;
    }

    private void validateLocalAuthority(int? localAuthorityId, List<int> allowedLocalAuthorityIds)
    {
        // Validate LocalAuthority if provided
        if (localAuthorityId != null)
        {
            // If not 'all', must match one of the allowed LocalAuthorities
            if (!allowedLocalAuthorityIds.Contains(0) &&
                !allowedLocalAuthorityIds.Contains(localAuthorityId.Value))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to search applications for this local authority");
            }
        }
    }

    private async Task validateEstablishment(int? establishmentId, List<int> allowedLocalAuthorityIds)
    {
        // Validate Establishment if provided
        if (establishmentId != null)
        {
            // Get the local authority ID for the establishment and check permissions
            var localAuthorityId =
                await _applicationGateway.GetLocalAuthorityIdForEstablishment(establishmentId.Value);

            // If not 'all', must match one of the allowed LocalAuthorities
            if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to search applications for this establishment's local authority");
            }
        }
    }
}