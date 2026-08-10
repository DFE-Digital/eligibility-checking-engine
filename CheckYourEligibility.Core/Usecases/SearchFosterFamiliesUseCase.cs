using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Constants.ErrorMessages;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface ISearchFosterFamiliesUseCase
{
    Task<FosterFamiliesSearchResponse> Execute(FosterFamiliesSearchRequest request, int localAuthorityId);
}

public class SearchFosterFamiliesUseCase : ISearchFosterFamiliesUseCase
{
    private readonly IFosterFamilies _gateway;

    public SearchFosterFamiliesUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamiliesSearchResponse> Execute(FosterFamiliesSearchRequest request, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if(request.PageNumber <= 0)
        {
            throw new ValidationException(FosterFamilyValidationMessages.InvalidPageNumber);
        }

        if(request.PageSize <= 0 || request.PageSize > 10)
        {
            throw new ValidationException(FosterFamilyValidationMessages.InvalidPageSize);
        }

        var response = await _gateway.SearchFosterFamilies(localAuthorityId, request);
        return response ?? new FosterFamiliesSearchResponse { Data = Enumerable.Empty<FosterFamiliesSearchItemResponse>() };
    }
}