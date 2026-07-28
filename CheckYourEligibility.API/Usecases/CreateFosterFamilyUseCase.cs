using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterFamilyUseCase
{
    Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request);
}

public class CreateFosterFamilyUseCase : ICreateFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;

    public CreateFosterFamilyUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _gateway.CreateFosterFamily(request);
        return response;
    }
}