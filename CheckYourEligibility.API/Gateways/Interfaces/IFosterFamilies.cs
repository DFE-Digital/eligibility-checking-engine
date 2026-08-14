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
        int localAuthorityId,
        UpdateFosterCarerRequest request);

    Task DeleteFosterCarer(Guid fosterCarerId, int localAuthorityId);

    Task DeleteFosterPartner(Guid fosterCarerId, int localAuthorityId);

     Task<FosterFamiliesSearchResponse> SearchFosterFamilies( 
        int localAuthorityId,
         FosterFamiliesSearchRequest request);


    // FosterChild

    Task<FosterChildResponse?> GetFosterChild(
        Guid fosterChildId,
        int localAuthorityId,
        bool includeFosterCarer = false);

    Task<FosterChildCreatedResponse> CreateFosterChild(
        FosterChildRequest request, int localAutorityId, Guid fosterCarerId, DateTime submissionDate);

    Task<FosterChildResponse> UpdateFosterChild(
        Guid fosterChildId,
        int localAuthorityId,
        UpdateFosterChildRequest request);

    Task DeleteFosterChild(
        Guid fosterChildId,
        int localAuthorityId);

}