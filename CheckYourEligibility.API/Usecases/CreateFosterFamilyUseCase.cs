using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;

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
            throw new ValidationException(FosterFamilyValidationMessages.LocalAuthorityId);
        }

        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                            FosterFamilyValidationMessages.CreateFosterFamilyPermission);
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