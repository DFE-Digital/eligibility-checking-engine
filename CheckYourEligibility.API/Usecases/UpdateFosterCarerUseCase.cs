using FluentValidation;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterCarerUseCase
{
    Task Execute(Guid fosterCarerId, List<int> localAuthorityIds, int localAuthorityId, UpdateFosterCarerRequest request);
}

public class UpdateFosterCarerUseCase : IUpdateFosterCarerUseCase
{
    private readonly IFosterFamilies _gateway;
    public UpdateFosterCarerUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task Execute(Guid fosterCarerId, List<int> localAuthorityIds, int localAuthorityId, UpdateFosterCarerRequest request)
    {
        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        ArgumentNullException.ThrowIfNull(request);

        if (localAuthorityId == null) throw new ValidationException(FosterFamilyValidationMessages.LocalAuthorityId);

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                            FosterFamilyValidationMessages.UpdateFosterCarerPermission);
        }

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

        await _gateway.UpdateFosterCarer(fosterCarerId, localAuthorityId, request);
    }
}