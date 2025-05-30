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
///     Interface for processing a single 2YO eligibility check
/// </summary>
public interface ICheckEligibilityFor2yoUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="model">2YO eligibility check request</param>
    /// <returns>Check eligibility response or validation errors</returns>
    Task<CheckEligibilityResponse> Execute(CheckEligibilityRequest_2yo model);
}

public class CheckEligibilityFor2yoUseCase : ICheckEligibilityFor2yoUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityFor2yoUseCase> _logger;
    private readonly IValidator<CheckEligibilityRequestData_2yo> _validator;

    public CheckEligibilityFor2yoUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        IValidator<CheckEligibilityRequestData_2yo> validator,
        ILogger<CheckEligibilityFor2yoUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _validator = validator;
        _logger = logger;
    }


    public async Task<CheckEligibilityResponse> Execute(CheckEligibilityRequest_2yo model)
    {
        if (model == null || model.Data == null)
            throw new ValidationException(null, "Invalid Request, data is required.");
        if (model.GetType() != typeof(CheckEligibilityRequest_2yo))
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