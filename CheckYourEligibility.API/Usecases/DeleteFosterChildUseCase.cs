using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

namespace CheckYourEligibility.API.UseCases;

public interface IDeleteFosterChildUseCase
{
    Task Execute(Guid fosterChildId, int localAuthorityId);
}

public class DeleteFosterChildUseCase : IDeleteFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public DeleteFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterChildId, int localAuthorityId)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterChildId);

        await _gateway.DeleteFosterChild(fosterChildId, localAuthorityId);
    }
}