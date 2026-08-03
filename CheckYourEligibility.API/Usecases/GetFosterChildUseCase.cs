using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

namespace CheckYourEligibility.API.UseCases;

public interface IGetFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId, List<int> localAuthorityIds, int localAuthorityId, bool includeFosterCarer = false);
}

public class GetFosterChildUseCase : IGetFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public GetFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, List<int> localAuthorityIds, int localAuthorityId, bool includeFosterCarer = false)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterChildId);

        if(localAuthorityId == null) throw new ValidationException(FosterFamilyValidationMessages.LocalAuthorityId);
        
        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                            FosterFamilyValidationMessages.GetFosterChildPermission);
        };

        var result = await _gateway.GetFosterChild(fosterChildId, localAuthorityId, includeFosterCarer);
        if (result == null) throw new KeyNotFoundException($"Foster child {fosterChildId} not found");

        return result;
    }
}