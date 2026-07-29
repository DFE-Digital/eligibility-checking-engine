using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterFamilyUseCase
{
    Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request, List<int> localAuthorityIds, int localAuthorityId);
}

public class CreateFosterFamilyUseCase : ICreateFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;

    public CreateFosterFamilyUseCase(
        IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request, List<int> localAuthorityIds, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (localAuthorityId <= 0)
        {
            throw new ValidationException("Local Authority ID is required");
        }

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to create a foster family for this Local Authority");
        }
        ;

        var validator = new FosterFamilyRequestValidator();
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(
                validationResult.Errors);
        }

        request.FosterCarer.LocalAuthorityID = localAuthorityId;

        return await _gateway.CreateFosterFamily(request);
    }
}