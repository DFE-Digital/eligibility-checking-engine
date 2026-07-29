using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IGetFosterFamilyUseCase
{
    Task<FosterFamilyResponse> Execute(Guid fosterCarerId, List<int> localAuthorityIds, int localAuthorityId, bool includeChildren = false);
}

public class GetFosterFamilyUseCase : IGetFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;

    public GetFosterFamilyUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamilyResponse> Execute(Guid fosterCarerId, List<int> localAuthorityIds, int localAuthorityId, bool includeChildren = false)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException("A valid fosterCarerId is required");

        if (localAuthorityId <= 0)
        {
            throw new ValidationException("Local Authority ID is required");
        }

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to get a foster family for this Local Authority");
        }
        ;

        var result = await _gateway.GetFosterFamily(fosterCarerId, localAuthorityId, includeChildren);
        return result;
    }
}