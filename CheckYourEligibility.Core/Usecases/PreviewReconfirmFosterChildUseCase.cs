using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface IPreviewReconfirmFosterChildUseCase
{
    Task<FosterCodePreviewResponse> Execute(Guid fosterChildId, FosterChildReconfirmRequest request, int localAuthorityId);
}

public class PreviewReconfirmFosterChildUseCase : IPreviewReconfirmFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public PreviewReconfirmFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterCodePreviewResponse> Execute(Guid fosterChildId, FosterChildReconfirmRequest request, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterChildReconfirmRequestValidator();
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        var response = await _gateway.PreviewReconfirmFosterChild(fosterChildId, localAuthorityId, request.SubmissionDate);
        return response;
    }
}