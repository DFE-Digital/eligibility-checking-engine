using System;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IDeleteFosterChildUseCase
{
    Task Execute(Guid fosterChildId);
}

public class DeleteFosterChildUseCase : IDeleteFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public DeleteFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterChildId)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException("A valid fosterChildId is required");

        await _gateway.DeleteFosterChild(fosterChildId);
    }
}