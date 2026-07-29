using FluentValidation;

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
        if (fosterCarerId == Guid.Empty) throw new ValidationException("A valid fosterCarerId is required");

        ArgumentNullException.ThrowIfNull(request);

        if (localAuthorityId == null) throw new ValidationException("Local Authority ID is required");

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to create a foster family for this Local Authority");
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