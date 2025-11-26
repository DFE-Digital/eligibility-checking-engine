using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using Establishment = CheckYourEligibility.API.Domain.Establishment;

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
    Task<ApplicationSearchResponse> GetApplications(ApplicationSearchRequest model);

    /// <summary>
    /// Updates an application
    /// </summary>
    /// <param name="guid">Application GUID</param>
    /// <param name="data">Update data</param>
    /// <returns>Update response</returns>
    Task<ApplicationUpdateResponse> UpdateApplication(string guid, ApplicationUpdateData data);

    /// <summary>
    /// Updates an application by reference
    /// </summary>
    /// <param name="reference">Application Reference</param>
    /// <param name="data">Update data</param>
    /// <returns>Update response</returns>
    Task<ApplicationUpdateResponse> UpdateApplicationByReference(string reference, ApplicationUpdateData data);

    /// <summary>
    /// Gets the local authority ID for an establishment
    /// </summary>
    /// <param name="establishmentId">Establishment ID</param>
    /// <returns>Local authority ID</returns>
    Task<int> GetLocalAuthorityIdForEstablishment(int establishmentId);

    /// <summary>
    /// Gets the multi academy trust ID for an establishment
    /// </summary>
    /// <param name="establishmentId">Establishment ID</param>
    /// <returns>multi academy trust ID</returns>
    Task<int> GetMultiAcademyTrustIdForEstablishment(int establishmentId);

    /// <summary>
    /// Gets the local authority ID for an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Local authority ID</returns>
    Task<int> GetLocalAuthorityIdForApplication(string applicationId);

    /// <summary>
    /// Gets the local authority ID for an application by reference
    /// </summary>
    /// <param name="reference">Application Reference</param>
    /// <returns>Local authority ID</returns>
    Task<int> GetLocalAuthorityIdForApplicationByReference(string reference);

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
    Task<Establishment?> GetEstablishmentEntityByUrn(string urn);

    /// <summary>
    /// Gets multiple establishment entities by their URNs in bulk
    /// </summary>
    /// <param name="urns">Collection of School URNs as strings</param>
    /// <returns>Dictionary mapping URN to establishment entity</returns>
    Task<Dictionary<string, Establishment>> GetEstablishmentEntitiesByUrns(IEnumerable<string> urns);
    
    /// <summary>
    /// Deletes an application by GUID
    /// </summary>
    /// <param name="guid">Application GUID</param>
    /// <returns>True if deleted successfully, false if not found</returns>
    Task<bool> DeleteApplication(string guid);

    /// <summary>
    /// Restores an archived application's previous status   
    /// </summary>
    /// <param name="guid">Application GUID</param>
    /// <returns>Task</returns>
    Task<ApplicationStatusRestoreResponse> RestoreArchivedApplicationStatus(string guid);
}