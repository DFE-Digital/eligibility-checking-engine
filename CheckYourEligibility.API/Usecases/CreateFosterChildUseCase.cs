using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterChildUseCase
{
    Task<FosterChildCreatedResponse> Execute(FosterChildRequest request, int localAuthorityId, Guid fosterCarerId, DateTime submissionDate);
}

public class CreateFosterChildUseCase : ICreateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;

    public CreateFosterChildUseCase(IFosterFamilies gateway)
    {
        _gateway = gateway;
    }

    public async Task<FosterChildCreatedResponse> Execute(FosterChildRequest request, int localAuthorityId, Guid fosterCarerId, DateTime submissionDate)
    {

        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterChildRequestValidator();
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(
                validationResult.Errors);
        }

        var response = await _gateway.CreateFosterChild(request, localAuthorityId, fosterCarerId, submissionDate);
        return response;
    }
}