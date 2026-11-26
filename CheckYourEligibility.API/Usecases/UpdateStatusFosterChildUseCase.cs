using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateStatusFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, string status);
}

public class UpdateStatusFosterChildUseCase : IUpdateStatusFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public UpdateStatusFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, string status)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterChildId);

        return await _gateway.UpdateStatusFosterChild(fosterChildId, localAuthorityId, status);
    }
}
