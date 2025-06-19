using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

/// <summary>
/// Interface for application data access operations
/// </summary>
public interface IApplication
{
    /// <summary>
    /// Creates a new application
    /// </summary>
    /// <param name="data">Application request data</param>
    /// <returns>Application response</returns>
    Task<ApplicationResponse> PostApplication(ApplicationRequestData data);
    
    /// <summary>
    /// Gets an application by GUID
    /// </summary>
    /// <param name="guid">Application GUID</param>
    /// <returns>Application response or null if not found</returns>
    Task<ApplicationResponse?> GetApplication(string guid);
    
    /// <summary>
    /// Searches for applications based on criteria
    /// </summary>
    /// <param name="model">Search criteria</param>
    /// <returns>Search results</returns>
    Task<ApplicationSearchResponse> GetApplications(ApplicationRequestSearch model);
    
    /// <summary>
    /// Updates the status of an application
    /// </summary>
    /// <param name="guid">Application GUID</param>
    /// <param name="data">Status update data</param>
    /// <returns>Status update response</returns>
    Task<ApplicationStatusUpdateResponse> UpdateApplicationStatus(string guid, ApplicationStatusData data);
    
    /// <summary>
    /// Gets the local authority ID for an establishment
    /// </summary>
    /// <param name="establishmentId">Establishment ID</param>
    /// <returns>Local authority ID</returns>
    Task<int> GetLocalAuthorityIdForEstablishment(int establishmentId);
    
    /// <summary>
    /// Gets the local authority ID for an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Local authority ID</returns>
    Task<int> GetLocalAuthorityIdForApplication(string applicationId);
    
    /// <summary>
    /// Bulk imports applications without creating eligibility check hashes
    /// </summary>
    /// <param name="applications">Collection of applications to import</param>
    /// <returns>Task</returns>
    Task BulkImportApplications(IEnumerable<Application> applications);

    /// <summary>
    /// Gets establishment entity by URN (Unique Reference Number)
    /// </summary>
    /// <param name="urn">School URN as string</param>
    /// <returns>Establishment entity or null if not found</returns>
    Task<CheckYourEligibility.API.Domain.Establishment?> GetEstablishmentEntityByUrn(string urn);
    
    /// <summary>
    /// Gets multiple establishment entities by their URNs in bulk
    /// </summary>
    /// <param name="urns">Collection of School URNs as strings</param>
    /// <returns>Dictionary mapping URN to establishment entity</returns>
    Task<Dictionary<string, CheckYourEligibility.API.Domain.Establishment>> GetEstablishmentEntitiesByUrns(IEnumerable<string> urns);
}