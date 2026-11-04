using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, UpdateFosterChildRequest request);
}

public class UpdateFosterChildUseCase : IUpdateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;
    private readonly bool _enforceSubmissionDateWindow;

    public UpdateFosterChildUseCase(IFosterFamilies gateway, IConfiguration? configuration = null)
    {
        _gateway = gateway;
        _enforceSubmissionDateWindow = configuration?.GetValue("FosterFamilies:EnforceSubmissionDateWindow", true) ?? true;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, UpdateFosterChildRequest request)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterChildId);
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = new FosterChildRequestValidator(_enforceSubmissionDateWindow).Validate(request.FosterChildRequest);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }


        var response = await _gateway.UpdateFosterChild(fosterChildId, localAuthorityId, request);
        return response;
    }
}