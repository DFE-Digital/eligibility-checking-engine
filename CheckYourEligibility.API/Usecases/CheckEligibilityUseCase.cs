using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using FeatureManagement.Domain.Validation;
using FluentValidation;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for processing a single eligibility check
/// </summary>
public interface ICheckEligibilityUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="model">Eligibility check request</param>
    /// <returns>Check eligibility response or validation errors</returns>

    Task<CheckEligibilityResponse> Execute(CheckEligibilityRequest model);

}

public class CheckEligibilityUseCase : ICheckEligibilityUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityUseCase> _logger;
    private readonly IValidator<CheckEligibilityRequestData> _validator;
    private readonly IServiceProvider _serviceProvider;
    public CheckEligibilityUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        IValidator<CheckEligibilityRequestData> validator,
        ILogger<CheckEligibilityUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _validator = validator;
        _logger = logger;
    }

    public async Task<CheckEligibilityResponse> Execute(CheckEligibilityRequest model)         
    {
        if (model == null || model.Data == null)
            throw new ValidationException(null, "Invalid Request, data is required.");

        // Normalize and validate the request
        model.Data.NationalInsuranceNumber = model.Data.NationalInsuranceNumber?.ToUpper();
        model.Data.NationalAsylumSeekerServiceNumber = model.Data.NationalAsylumSeekerServiceNumber?.ToUpper();

        var validationResults = _validator.Validate(model.Data);

        if (!validationResults.IsValid) throw new ValidationException(null, validationResults.ToString());

        // Execute the check
        var response = await _checkGateway.PostCheck(model.Data);
        if (response != null)
        {
            await _auditGateway.CreateAuditEntry(AuditType.Check, response.Id);
            _logger.LogInformation($"Eligibility check created with ID: {response.Id}");
            return new CheckEligibilityResponse
            {
                Data = new StatusValue { Status = response.Status.ToString() },
                Links = new CheckEligibilityResponseLinks
                {
                    Get_EligibilityCheck = $"{CheckLinks.GetLink}{response.Id}",
                    Put_EligibilityCheckProcess = $"{CheckLinks.ProcessLink}{response.Id}",
                    Get_EligibilityCheckStatus = $"{CheckLinks.GetLink}{response.Id}/status"
                }
            };
        }

        _logger.LogWarning("Response for eligibility check was null.");
        throw new ValidationException(null, "Eligibility check not completed successfully.");
    }

}