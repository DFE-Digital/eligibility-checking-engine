using System.ComponentModel.DataAnnotations;
using Azure;
using CheckYourEligibility.API.Gateways.Interfaces;
using System.Text.Json;

public interface ICreateEligibilityCheckReportUseCase
{
    /// <summary>
    /// Generates a reports for checks based on the provided request model
    /// </summary>
    /// <param name="model">The request model containing parameters for report generation</param>
    /// <returns>A stream containing the generated report</returns>
    Task<string> Execute(EligibilityCheckReportRequest model);
}

public class CreateEligibilityCheckReportUseCase : ICreateEligibilityCheckReportUseCase
{
    private readonly IEligibilityReporting _eligibilityReportingGateway;
    private readonly ILogger<CreateEligibilityCheckReportUseCase> _logger;

    public CreateEligibilityCheckReportUseCase(IEligibilityReporting eligibilityReportingGateway, ILogger<CreateEligibilityCheckReportUseCase> logger)
    {
        _eligibilityReportingGateway = eligibilityReportingGateway;
        _logger = logger;
    }

    public async Task<string> Execute(EligibilityCheckReportRequest model)
    {
        if (model == null) throw new ValidationException("Invalid request, model is required");

        var validator = new EligibilityCheckReportRequestValidator();
        var validationResults = validator.Validate(model);

        if (!validationResults.IsValid) throw new ValidationException(validationResults.ToString());

        var response = await _eligibilityReportingGateway.CreateEligibilityCheckReport(model);

        if (response == null)
        {
            var sanitizedRequest = JsonSerializer.Serialize(model);
            _logger.LogError("Failed to generate eligibility check report for request: {SanitizedRequest}", sanitizedRequest);
            throw new Exception("Failed to generate eligibility check report");
        }

        _logger.LogInformation("Successfully generated eligibility check report");

        return response;
    }

}