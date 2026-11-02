using CheckYourEligibility.Core.Domain.Constants.ErrorMessages;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;
using FluentValidation;

namespace CheckYourEligibility.Core.UseCases;

public interface ICreateFosterChildUseCase
{
    Task<FosterChildResponse> Execute(FosterChildRequest request, int localAuthorityId, Guid fosterCarerId, DateTime submissionDate);
}

public class CreateFosterChildUseCase : ICreateFosterChildUseCase
{
    private readonly IFosterFamilies _gateway;
    private readonly bool _enforceSubmissionDateWindow;

    public CreateFosterChildUseCase(IFosterFamilies gateway, IConfiguration? configuration = null)
    {
        _gateway = gateway;
        _enforceSubmissionDateWindow = configuration?.GetValue("FosterFamilies:EnforceSubmissionDateWindow", true) ?? true;
    }

    public async Task<FosterChildResponse> Execute(FosterChildRequest request, int localAuthorityId, Guid fosterCarerId, DateTime submissionDate)
    {

        if (fosterCarerId == Guid.Empty) throw new ValidationException(FosterFamilyValidationMessages.FosterCarerId);

        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterChildRequestValidator(_enforceSubmissionDateWindow);
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