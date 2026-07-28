using System;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IDeleteFosterCarerUseCase
{
    Task Execute(Guid fosterCarerId);
}

public class DeleteFosterCarerUseCase : IDeleteFosterCarerUseCase
{
    private readonly IFosterFamilies _gateway;

    public DeleteFosterCarerUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterCarerId)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException("A valid fosterCarerId is required");

        await _gateway.DeleteFosterCarer(fosterCarerId);
    }
}