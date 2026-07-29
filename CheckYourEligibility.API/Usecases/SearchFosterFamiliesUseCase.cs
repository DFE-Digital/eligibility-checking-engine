using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.UseCases;

public interface ISearchFosterFamiliesUseCase
{
    Task<FosterFamiliesSearchResponse> Execute(FosterFamiliesSearchRequest request, int localAuthorityId, List<int> localAuthorityIds);
}

public class SearchFosterFamiliesUseCase : ISearchFosterFamiliesUseCase
{
    private readonly IFosterFamilies _gateway;

    public SearchFosterFamiliesUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamiliesSearchResponse> Execute(FosterFamiliesSearchRequest request, int localAuthorityId, List<int> localAuthorityIds)
    {
        ArgumentNullException.ThrowIfNull(request);

         if(localAuthorityId == null) throw new ValidationException("Local Authority ID is required");
        
        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to search foster families for this Local Authority");
        };

        var response = await _gateway.SearchFosterFamilies(localAuthorityId, request);
        return response ?? new FosterFamiliesSearchResponse { Data = Enumerable.Empty<FosterFamiliesSearchItemResponse>() };
    }
}