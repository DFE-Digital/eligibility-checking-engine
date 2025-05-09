using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using FeatureManagement.Domain.Validation;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateApplicationUseCase
{
    Task<ApplicationSaveItemResponse> Execute(ApplicationRequest model);
}

public class CreateApplicationUseCase : ICreateApplicationUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;

    public CreateApplicationUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }

    public async Task<ApplicationSaveItemResponse> Execute(ApplicationRequest model)
    {
        if (model == null || model.Data == null) throw new ValidationException("Invalid request, data is required");
        if (model.Data.Type == CheckEligibilityType.None)
            throw new ValidationException($"Invalid request, Valid Type is required: {model.Data.Type}");

        model.Data.ParentNationalInsuranceNumber = model.Data.ParentNationalInsuranceNumber?.ToUpper();
        model.Data.ParentNationalAsylumSeekerServiceNumber =
            model.Data.ParentNationalAsylumSeekerServiceNumber?.ToUpper();

        var validator = new ApplicationRequestValidator();
        var validationResults = validator.Validate(model);

        if (!validationResults.IsValid) throw new ValidationException(validationResults.ToString());

        var response = await _applicationGateway.PostApplication(model.Data);

        if (response == null)
            throw new Exception("PostApplication returned null");
        
        await _auditGateway.CreateAuditEntry(AuditType.Application, response.Id);

        return new ApplicationSaveItemResponse
        {
            Data = response,
            Links = new ApplicationResponseLinks
            {
                get_Application = $"{ApplicationLinks.GetLinkApplication}{response?.Id}"
            }
        };
    }
}