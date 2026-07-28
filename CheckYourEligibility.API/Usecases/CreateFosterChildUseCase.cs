using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterChildUseCase
{
    Task<FosterChildCreatedResponse> Execute(FosterChildRequest request, Guid fosterCarerId, DateTime submissionDate);
}

public class CreateFosterChildUseCase : ICreateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public CreateFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildCreatedResponse> Execute(FosterChildRequest request, Guid fosterCarerId, DateTime submissionDate)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (fosterCarerId == Guid.Empty) throw new ValidationException("A valid fosterCarerId is required");

        var response = await _gateway.CreateFosterChild(request, fosterCarerId, submissionDate);
        return response;
    }
}