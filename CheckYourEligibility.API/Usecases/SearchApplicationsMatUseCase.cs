using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using System;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for the search applications use case
/// </summary>
public interface ISearchApplicationsMatUseCase
{
    /// <summary>
    /// Searches for applications after validating local authority permissions
    /// </summary>
    /// <param name="model">The application search request data</param>
    /// <param name="allowedMultiAcademyTrustIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The search results response</returns>
    Task<ApplicationSearchResponse> Execute(ApplicationRequestSearch model, List<int> allowedMultiAcademyTrustIds);
}

/// <summary>
/// Implementation of the search applications use case
/// </summary>
public class SearchApplicationsMatUseCase : ISearchApplicationsUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    /// <summary>
    /// Constructor for SearchApplicationsUseCase
    /// </summary>
    /// <param name="applicationGateway">The application gateway</param>
    /// <param name="auditGateway">The audit gateway</param>
    public SearchApplicationsMatUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }

    /// <summary>
    /// Searches for applications after validating local authority permissions
    /// </summary>
    /// <param name="model">The application search request data</param>
    /// <param name="allowedMultiAcademyTrustIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The search results response</returns>
    public async Task<ApplicationSearchResponse> Execute(ApplicationRequestSearch model,
        List<int> allowedMultiAcademyTrustIds)
    {
        if (model?.Data == null)
        {
            throw new ArgumentException("Invalid request, data is required");
        }

        // Either MultiAcademyTrust or Establishment or both must be provided
        if (model.Data.MultiAcademyTrust == null && model.Data.Establishment == null)
        {
            throw new ArgumentException("Either MultiAcademyTrust or Establishment must be specified");
        }

        // Validate MultiAcademyTrust if provided
        if (model.Data.MultiAcademyTrust != null)
        {
            // If not 'all', must match one of the allowed LocalAuthorities
            if (!allowedMultiAcademyTrustIds.Contains(0) &&
                !allowedMultiAcademyTrustIds.Contains(model.Data.MultiAcademyTrust.Value))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to search applications for this multi academy trust");
            }
        }

        // Validate Establishment if provided
        if (model.Data.Establishment != null)
        {
            // Get the local authority ID for the establishment and check permissions
            var multiAcademyTrustId =
                await _applicationGateway.GetMultiAcademyTrustIdForEstablishment(model.Data.Establishment.Value);

            // If not 'all', must match one of the allowed LocalAuthorities
            if (!allowedMultiAcademyTrustIds.Contains(0) && !allowedMultiAcademyTrustIds.Contains(multiAcademyTrustId))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to search applications for this establishment's multi academy trust");
            }
        }

        var response = await _applicationGateway.GetApplications(model);

        if (response == null || !response.Data.Any())
            return new ApplicationSearchResponse { Data = [], TotalPages = 0, TotalRecords = 0 };
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);

        return response;
    }
}