using System.Text;
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
///     Interface for processing a single EYPP eligibility check
/// </summary>
public interface ICheckEligibilityForEyppUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="model">EYPP eligibility check request</param>
    /// <returns>Check eligibility response or validation errors</returns>
    Task<CheckEligibilityResponse> Execute(CheckEligibilityRequest_Eypp model);
}


public class CheckEligibilityForEyppUseCase : ICheckEligibilityForEyppUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityForEyppUseCase> _logger;
    private readonly IValidator<CheckEligibilityRequestData_Eypp> _validator;
    public CheckEligibilityForEyppUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        IValidator<CheckEligibilityRequestData_Eypp> validator,
        ILogger<CheckEligibilityForEyppUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _validator = validator;
        _logger = logger;
    }


    public async Task<CheckEligibilityResponse> Execute(CheckEligibilityRequest_Eypp model)
    {
        if (model == null || model.Data == null)
            throw new ValidationException(null, "Invalid Request, data is required.");
        if (model.GetType() != typeof(CheckEligibilityRequest_Eypp))
            throw new ValidationException(null, $"Unknown request type:-{model.GetType()}");

        // Normalize and validate the request
        model.Data.NationalInsuranceNumber = model.Data.NationalInsuranceNumber?.ToUpper();
        
        var validationResults = _validator.Validate(model.Data);

        if (!validationResults.IsValid) throw new ValidationException(null, validationResults.ToString());

        // Execute the check
        var response = await _checkGateway.PostCheck(model.Data);
        if (response != null)
        {
            await _auditGateway.CreateAuditEntry(AuditType.Check, response.Id);
            _logger.LogInformation($"FSM eligibility check created with ID: {response.Id}");
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

        _logger.LogWarning("Response for FSM eligibility check was null.");
        throw new ValidationException(null, "Eligibility check not completed successfully.");
    }
}