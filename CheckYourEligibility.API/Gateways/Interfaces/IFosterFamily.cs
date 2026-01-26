public interface IFosterFamily
{
    /// <summary>
    /// Creates a new foster family application
    /// </summary>
    /// <param name="data">Foster family request data</param>
    /// <returns>Foster family application response</returns>
    Task<FosterFamilyResponse> PostFosterFamily(FosterFamilyRequestData data, CancellationToken cancellationToken = default);
}