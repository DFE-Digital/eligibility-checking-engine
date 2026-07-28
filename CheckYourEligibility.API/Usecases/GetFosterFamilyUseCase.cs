using System;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IGetFosterFamilyUseCase
{
    Task<FosterFamilyResponse> Execute(Guid fosterCarerId, bool includeChildren = false);
}

public class GetFosterFamilyUseCase : IGetFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;

    public GetFosterFamilyUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamilyResponse> Execute(Guid fosterCarerId, bool includeChildren = false)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException("A valid fosterCarerId is required");

        var result = await _gateway.GetFosterFamily(fosterCarerId, includeChildren);
        return result;
    }
}