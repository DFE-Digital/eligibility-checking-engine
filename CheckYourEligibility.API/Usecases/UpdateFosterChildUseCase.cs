using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId, UpdateFosterChildRequest request);
}

public class UpdateFosterChildUseCase : IUpdateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public UpdateFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, UpdateFosterChildRequest request)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException("A valid fosterChildId is required");
        ArgumentNullException.ThrowIfNull(request);

        var response = await _gateway.UpdateFosterChildAsync(fosterChildId, request);
        return response;
    }
}