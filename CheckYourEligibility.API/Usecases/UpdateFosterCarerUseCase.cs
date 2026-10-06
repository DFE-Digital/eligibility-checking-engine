using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterCarerUseCase
{
    Task Execute(Guid fosterCarerId, int localAuthorityId, UpdateFosterCarerRequest request);
}

public class UpdateFosterCarerUseCase : IUpdateFosterCarerUseCase
{
    private readonly IFosterFamilies _gateway;
    public UpdateFosterCarerUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterCarerId, int localAuthorityId, UpdateFosterCarerRequest request)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        ArgumentNullException.ThrowIfNull(request);


        if (request.FosterCarerRequest is not null)
        {
            var validationResult =
                new FosterCarerRequestValidator()
                    .Validate(request.FosterCarerRequest);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
        }

        if (request.FosterPartnerRequest is not null)
        {
            var validationResult =
                new FosterPartnerRequestValidator()
                    .Validate(request.FosterPartnerRequest);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
        }

        if (request.FosterCarerRequest is not null)
        {
            request.FosterCarerRequest.CarerNationalInsuranceNumber =
                NinoValidation.Normalize(
                    request.FosterCarerRequest.CarerNationalInsuranceNumber);
        }

        if (request.FosterPartnerRequest is not null)
        {
            request.FosterPartnerRequest.PartnerNationalInsuranceNumber =
                NinoValidation.Normalize(
                    request.FosterPartnerRequest.PartnerNationalInsuranceNumber);
        }

        await _gateway.UpdateFosterCarer(fosterCarerId, localAuthorityId, request);
    }
}