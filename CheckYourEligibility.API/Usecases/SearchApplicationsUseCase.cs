using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.IdentityModel.Tokens;

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
    Task<ApplicationSearchResponse> Execute(ApplicationSearchRequest model, List<int> allowedLocalAuthorityIds, List<int> allowedMultiAcademyTrustIds, List<int> allowedEstablishmentIds);
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
    public async Task<ApplicationSearchResponse> Execute(ApplicationSearchRequest model,
        List<int> allowedLocalAuthorityIds, List<int> allowedMultiAcademyTrustIds, List<int> allowedEstablishmentIds)
    {
        if (model?.Data == null)
        {
            throw new ArgumentException("Invalid request, data is required");
        }

        // Either LocalAuthority, Establishment, or MultiAcademyTrust or combination must be provided
        if (!model.Data.LocalAuthority.HasValue && !model.Data.Establishment.HasValue && !model.Data.MultiAcademyTrust.HasValue)
        {
            throw new ArgumentException("Either LocalAuthority, Establishment, or MultiAcademyTrust must be specified");
        }

        if (model.Data.LocalAuthority.HasValue)
            validateLocalAuthority(model.Data.LocalAuthority.Value, allowedLocalAuthorityIds);
        if (model.Data.MultiAcademyTrust.HasValue)
            validateMultiAcademyTrust(model.Data.MultiAcademyTrust.Value, allowedMultiAcademyTrustIds);
        if (model.Data.Establishment.HasValue)
            await validateEstablishment(model.Data.Establishment.Value, allowedLocalAuthorityIds,
                allowedMultiAcademyTrustIds, allowedEstablishmentIds);
        
        var response = await _applicationGateway.GetApplications(model);

        if (response == null || !response.Data.Any())
            return new ApplicationSearchResponse { Data = [], TotalPages = 0, TotalRecords = 0, Meta = new ApplicationSearchResponseMeta(){TotalPages = 0, TotalRecords = 0} };
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);

        return response;
    }

    private void validateLocalAuthority(int localAuthorityId, List<int> allowedLocalAuthorityIds)
    {
        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) &&
            !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to search applications for this local authority");
        }
    }

    private void validateMultiAcademyTrust(int multiAcademyTrustId, List<int> allowedMultiAcademyTrustIds)
    {
        // If not 'all', must match one of the allowed MultiAcademyTrusts
        if (!allowedMultiAcademyTrustIds.Contains(0) &&
            !allowedMultiAcademyTrustIds.Contains(multiAcademyTrustId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to search applications for this multi academy trust");
        }
    }

    private async Task validateEstablishment(int establishmentId, List<int> allowedLocalAuthorityIds, List<int> allowedMultiAcademyTrustIds, List<int> allowedEstablishmentIds)
    {
        //This checks that
        //Does the establishment scope allow this search
        //Does the MAT scope allow this search
        //Does the LA scope allow this search
        // Returns at first success. Means that if valid establishment but invalid LA the search will still be allowed.

        //Check if an establishment scope was provided for Id used in the search
        if (allowedEstablishmentIds.Contains(0) || allowedEstablishmentIds.Contains(establishmentId))
        {
            // There is a matching establishment scope
            return;
        }
        else if (!allowedEstablishmentIds.IsNullOrEmpty())
        {
            //If an establishment scope was provided which didn't allow the establishment Id in the request
            throw new UnauthorizedAccessException(
                "You do not have permission to search applications for this establishment's local authority or multi academy trust");
        }
        
        var multiAcademyTrustId = await _applicationGateway.GetMultiAcademyTrustIdForEstablishment(establishmentId);
        //If the establishment belongs to a Multi Academy Trust, And there is a MAT scope that matches the MAT for the establishment
        if (multiAcademyTrustId != 0 && (allowedMultiAcademyTrustIds.Contains(0) || allowedMultiAcademyTrustIds.Contains(multiAcademyTrustId)))
        {
            // There is a matching establishment scope
            return;
        }
        //Else if there was an attempt to use a multi_academy_trust scope which didn't correspond to the establishment
        else if (!allowedEstablishmentIds.IsNullOrEmpty() && !allowedEstablishmentIds.Contains(0))
        {
            //If an establishment scope was provided which didn't allow the establishment Id in the request
            throw new UnauthorizedAccessException(
                "You do not have permission to search applications for this establishment's local authority or multi academy trust");
        }

        // Check that the establishment belongs to any of the LAs provided in the scopes
        var localAuthorityId =
            await _applicationGateway.GetLocalAuthorityIdForEstablishment(establishmentId);
        if (allowedLocalAuthorityIds.Contains(0) || allowedLocalAuthorityIds.Contains(localAuthorityId)){ 
            return;
        }
        else
        {
            //No scopes were present that have access to the establishment Id that was searched for
            throw new UnauthorizedAccessException(
                "You do not have permission to search applications for this establishment's local authority or multi academy trust");
        }
    }
}