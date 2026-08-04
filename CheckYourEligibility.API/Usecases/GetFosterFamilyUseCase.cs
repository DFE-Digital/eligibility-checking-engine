using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

namespace CheckYourEligibility.API.UseCases;

public interface IGetFosterFamilyUseCase
{
    Task<FosterFamilyResponse> Execute(Guid fosterCarerId, int localAuthorityId, bool includeChildren = false);
}

public class GetFosterFamilyUseCase : IGetFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;

    public GetFosterFamilyUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamilyResponse> Execute(Guid fosterCarerId, int localAuthorityId, bool includeChildren = false)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        var result = await _gateway.GetFosterFamily(fosterCarerId, localAuthorityId, includeChildren);
        return result;
    }
}