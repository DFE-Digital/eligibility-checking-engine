using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

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
        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        if (localAuthorityId <= 0)
        {
            throw new ValidationException(FosterFamilyValidationMessages.LocalAuthorityId);
        }

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                            FosterFamilyValidationMessages.GetFosterFamilyPermission);
        }
        ;

        var result = await _gateway.GetFosterFamily(fosterCarerId, localAuthorityId, includeChildren);
        return result;
    }
}