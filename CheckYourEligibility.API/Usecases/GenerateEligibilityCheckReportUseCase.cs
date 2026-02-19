using System.ComponentModel.DataAnnotations;
using Azure;
using CheckYourEligibility.API.Gateways.Interfaces;

public interface IGenerateEligibilityCheckReportUseCase
{
    /// <summary>
    /// Generates a reports for bulk checks based on the provided request model
    /// </summary>
    /// <param name="model">The request model containing parameters for report generation</param>
    /// <returns>A stream containing the generated report</returns>
    Task<EligibilityCheckReportResponse> Execute(EligibilityCheckReportRequest model);
}

public class GenerateEligibilityCheckReportUseCase : IGenerateEligibilityCheckReportUseCase
{
    private readonly  ICheckEligibility _checkEligibilityGateway;
    private readonly ILogger<GenerateEligibilityCheckReportUseCase> _logger;

    public GenerateEligibilityCheckReportUseCase(ICheckEligibility checkEligibilityGateway, ILogger<GenerateEligibilityCheckReportUseCase> logger)
    {
        _checkEligibilityGateway = checkEligibilityGateway;
        _logger = logger;
    }

    public async Task<EligibilityCheckReportResponse> Execute(EligibilityCheckReportRequest model)
    {
        if (model == null) throw new ValidationException("Invalid request, model is required");

        var validator = new EligibilityCheckReportRequestValidator();
        var validationResults = validator.Validate(model);

        if (!validationResults.IsValid) throw new ValidationException(validationResults.ToString());

        var response = await _checkEligibilityGateway.GenerateEligibilityCheckReports(model);

        if (response == null)
        {
            _logger.LogError("Failed to generate eligibility check report for request: {@Request}", model);
            throw new Exception("Failed to generate eligibility check report");
        } 

        _logger.LogInformation("Successfully generated eligibility check report");

        return new EligibilityCheckReportResponse
        {
           Data = response
        };
    }
}

