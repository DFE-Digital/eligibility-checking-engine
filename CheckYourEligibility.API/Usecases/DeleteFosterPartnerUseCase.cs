using System;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

namespace CheckYourEligibility.API.UseCases;

public interface IDeleteFosterPartnerUseCase
{
    Task Execute(Guid fosterCarerId);
}

public class DeleteFosterPartnerUseCase : IDeleteFosterPartnerUseCase
{
    private readonly IFosterFamilies _gateway;

    public DeleteFosterPartnerUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterCarerId)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        await _gateway.DeleteFosterPartner(fosterCarerId);
    }
}