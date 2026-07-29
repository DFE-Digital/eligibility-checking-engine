using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateFosterChildUseCase
{
    Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, List<int> localAuthorityIds, UpdateFosterChildRequest request);
}

public class UpdateFosterChildUseCase : IUpdateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public UpdateFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildResponse> Execute(Guid fosterChildId, int localAuthorityId, List<int> localAuthorityIds, UpdateFosterChildRequest request)
    {
        if (fosterChildId == Guid.Empty) throw new ValidationException("A valid fosterChildId is required");
        ArgumentNullException.ThrowIfNull(request);

        if (localAuthorityId == null) throw new ValidationException("Local Authority ID is required");

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to create a foster family for this Local Authority");
        }


        var validationResult = new FosterChildRequestValidator().Validate(request.FosterChildRequest);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }


        var response = await _gateway.UpdateFosterChild(fosterChildId, localAuthorityId, request);
        return response;
    }
}