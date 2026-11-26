using FluentValidation;
using CheckYourEligibility.Core.Domain.Constants.ErrorMessages;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface IDeleteFosterCarerUseCase
{
    Task Execute(Guid fosterCarerId, int localAuthorityId);
}

public class DeleteFosterCarerUseCase : IDeleteFosterCarerUseCase
{
    private readonly IFosterFamilies _gateway;

    public DeleteFosterCarerUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterCarerId, int localAuthorityId)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        await _gateway.DeleteFosterCarer(fosterCarerId, localAuthorityId);
    }
}