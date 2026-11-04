using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterFamilyUseCase
{
    Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request, int localAuthorityId);
}

public class CreateFosterFamilyUseCase : ICreateFosterFamilyUseCase
{
    private readonly IFosterFamilies _gateway;
    private readonly bool _enforceSubmissionDateWindow;

    public CreateFosterFamilyUseCase(IFosterFamilies gateway, IConfiguration? configuration = null)
    {
        _gateway = gateway;
        _enforceSubmissionDateWindow = configuration?.GetValue("FosterFamilies:EnforceSubmissionDateWindow", true) ?? true;
    }

    public async Task<FosterFamilyCreatedResponse> Execute(FosterFamilyRequest request, int localAuthorityId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validator = new FosterFamilyRequestValidator(_enforceSubmissionDateWindow);
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(
                validationResult.Errors);
        }

        request.FosterCarer.CarerNationalInsuranceNumber =
            NinoValidation.Normalize(request.FosterCarer.CarerNationalInsuranceNumber);

        if (request.HasPartner && request.Partner is not null)
        {
            request.Partner.PartnerNationalInsuranceNumber =
                NinoValidation.Normalize(request.Partner.PartnerNationalInsuranceNumber);
        }

        request.FosterCarer.LocalAuthorityID = localAuthorityId;

        return await _gateway.CreateFosterFamily(request);
    }
}