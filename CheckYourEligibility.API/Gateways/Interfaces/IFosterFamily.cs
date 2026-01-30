public interface IFosterFamily
{
    /// <summary>
    /// Creates a new foster family application
    /// </summary>
    /// <param name="data">Foster family request data</param>
    /// <returns>Foster family application response</returns>
    Task<FosterFamilyResponse> PostFosterFamily(FosterFamilyRequestData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an foster family by GUID
    /// </summary>
    /// <param name="guid">Foster family GUID</param>
    /// <returns>Foster family response or null if not found</returns>
    Task<FosterFamilyResponse?> GetFosterFamily(string guid);

    /// <summary>
    /// Updates an Foster Family
    /// </summary>
    /// <param name="guid">Foster Carer GUID</param>
    /// <param name="data">Update data</param>
    /// <returns>Update response</returns>
    Task<FosterFamilyResponse> UpdateFosterFamily(string guid, FosterFamilyUpdateRequest data, CancellationToken cancellationToken = default);
}