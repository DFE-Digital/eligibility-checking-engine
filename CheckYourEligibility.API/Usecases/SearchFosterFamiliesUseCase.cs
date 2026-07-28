using System;
using System.Linq;
using System.Collections.Generic;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface ISearchFosterFamiliesUseCase
{
    Task<FosterFamiliesSearchResponse> Execute(FosterFamiliesSearchRequest request);
}

public class SearchFosterFamiliesUseCase : ISearchFosterFamiliesUseCase
{
    private readonly IFosterFamilies _gateway;

    public SearchFosterFamiliesUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamiliesSearchResponse> Execute(FosterFamiliesSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _gateway.SearchFosterFamilies(request);
        return response ?? new FosterFamiliesSearchResponse { Data = Enumerable.Empty<FosterFamiliesSearchItemResponse>() };
    }
}