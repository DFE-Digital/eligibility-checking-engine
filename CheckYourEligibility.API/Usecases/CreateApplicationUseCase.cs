using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using FeatureManagement.Domain.Validation;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for the create application use case
/// </summary>
public interface ICreateApplicationUseCase
{
    /// <summary>
    /// Creates a new application after validating local authority permissions
    /// </summary>
    /// <param name="model">The application request data</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The created application response</returns>
    Task<ApplicationSaveItemResponse> Execute(ApplicationRequest model, List<int> allowedLocalAuthorityIds);
}

/// <summary>
/// Implementation of the create application use case
/// </summary>
public class CreateApplicationUseCase : ICreateApplicationUseCase
{
    private readonly IApplication _applicationGateway;
    private readonly IAudit _auditGateway;/// <summary>
                                          /// Constructor for CreateApplicationUseCase
                                          /// </summary>
                                          /// <param name="applicationGateway">The application gateway</param>
                                          /// <param name="auditGateway">The audit gateway</param>
    public CreateApplicationUseCase(IApplication applicationGateway, IAudit auditGateway)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
    }/// <summary>
     /// Creates a new application after validating local authority permissions
     /// </summary>
     /// <param name="model">The application request data</param>
     /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
     /// <returns>The created application response</returns>
    public async Task<ApplicationSaveItemResponse> Execute(ApplicationRequest model, List<int> allowedLocalAuthorityIds)
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

        // Get the local authority ID for the establishment and check permissions
        var localAuthorityId = await _applicationGateway.GetLocalAuthorityIdForEstablishment(model.Data.Establishment);

        // If not 'all', must match one of the allowed LocalAuthorities
        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
        {
            throw new UnauthorizedAccessException("You do not have permission to create applications for this establishment's local authority");
        }
        var response = await _applicationGateway.PostApplication(model.Data);
        if (response != null) await _auditGateway.CreateAuditEntry(AuditType.Application, response.Id);

        if (response == null)
        {
            throw new Exception("Failed to create application");
        }

        return new ApplicationSaveItemResponse
        {
            Data = response,
            Links = new ApplicationResponseLinks
            {
                get_Application = $"{ApplicationLinks.GetLinkApplication}{response.Id}"
            }
        };
    }
}