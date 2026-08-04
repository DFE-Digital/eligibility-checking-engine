using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, UpdateFosterChildRequest request);
}

public class UpdateFosterChildUseCase : IUpdateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public UpdateFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, UpdateFosterChildRequest request)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterChildId);
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = new FosterChildRequestValidator().Validate(request.FosterChildRequest);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }


        var response = await _gateway.UpdateFosterChild(fosterChildId, localAuthorityId, request);
        return response;
    }
}