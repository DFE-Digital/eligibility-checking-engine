public interface IFosterFamilies
{
    //FosterCarer

    Task<FosterFamilyResponse> GetFosterFamily(
        Guid fosterCarerId,
        int localAuthorityId,
        bool includeChildren = false);

    Task<FosterFamilyCreatedResponse> CreateFosterFamily(
        FosterFamilyRequest request);

    Task UpdateFosterCarer(
        Guid fosterCarerId,
        UpdateFosterCarerRequest request);

    Task DeleteFosterCarer(Guid fosterCarerId);

    Task DeleteFosterPartner(Guid fosterCarerId);

     Task<FosterFamiliesSearchResponse> SearchFosterFamilies(
         FosterFamiliesSearchRequest request);


    // FosterChild

    Task<FosterChildResponse?> GetFosterChild(
        Guid fosterChildId,
        bool includeFosterCarer = false);

    Task<FosterChildCreatedResponse> CreateFosterChild(
        FosterChildRequest request, Guid fosterCarerId, DateTime submissionDate);

    Task<FosterChildResponse> UpdateFosterChildAsync(
        Guid fosterChildId,
        UpdateFosterChildRequest request);

    Task DeleteFosterChild(
        Guid fosterChildId);

}