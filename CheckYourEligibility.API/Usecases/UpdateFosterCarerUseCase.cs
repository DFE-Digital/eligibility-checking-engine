using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterCarerUseCase
{
    Task Execute(Guid fosterCarerId, UpdateFosterCarerRequest request);
}

public class UpdateFosterCarerUseCase : IUpdateFosterCarerUseCase
{
    private readonly IFosterFamilies _gateway;

    public UpdateFosterCarerUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterCarerId, UpdateFosterCarerRequest request)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException("A valid fosterCarerId is required");
        ArgumentNullException.ThrowIfNull(request);

        await _gateway.UpdateFosterCarer(fosterCarerId, request);
    }
}