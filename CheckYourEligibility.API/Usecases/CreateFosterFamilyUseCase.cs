using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterFamilyUseCase
{
    Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request, List<int> localAuthorityIds, int localAuthorityId);
}

public class CreateFosterFamilyUseCase : ICreateFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;
    private readonly FosterFamilyRequestValidator _validator;

    public CreateFosterFamilyUseCase(
        IFosterFamilies gateway,
        FosterFamilyRequestValidator validator)
    {
        _gateway = gateway;
        _validator = validator;
    }

    public async Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request, List<int> localAuthorityIds, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if(localAuthorityId == null) throw new ValidationException("Local Authority ID is required");
        
        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to create a foster family for this Local Authority");
        };

        var validationResult = _validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(
                validationResult.Errors);
        }

        request.FosterCarer.LocalAuthorityID = localAuthorityId;

        return await _gateway.CreateFosterFamily(request);
    }
}