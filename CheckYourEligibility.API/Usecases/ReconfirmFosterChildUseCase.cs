using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Helpers;

namespace CheckYourEligibility.API.UseCases;

public interface IReconfirmFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId,FosterChildReconfirmRequest request, int localAuthorityId);
}

public class ReconfirmFosterChildUseCase : IReconfirmFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public ReconfirmFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, FosterChildReconfirmRequest request, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterChildReconfirmRequestValidator();
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        var response = await _gateway.ReconfirmFosterChild(fosterChildId, localAuthorityId, request.SubmissionDate);
        return response;
    }
}