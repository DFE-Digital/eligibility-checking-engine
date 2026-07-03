using CheckYourEligibility.API.Boundary.Requests;
using System.ComponentModel.DataAnnotations;

public interface IGetEligibilityCheckReportingUseCase
{
    /// <summary>
    /// Generates a reports for bulk checks based on the provided request model
    /// </summary>
    /// <param name="model">The request model containing parameters for report generation</param>
    /// <returns>A stream containing the generated report</returns>
    Task<EligibilityCheckReportResponse> Execute(EligibilityCheckReportRequest model, CheckMetaData meta);
}

public class GetEligibilityCheckReportingUseCase : IGetEligibilityCheckReportingUseCase
{
    private readonly  IEligibilityCheckReporting _eligibilityCheckReportingGateway;
    private readonly ILogger<GetEligibilityCheckReportingUseCase> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public GetEligibilityCheckReportingUseCase(IEligibilityCheckReporting eligibilityCheckReportingGateway, ILogger<GetEligibilityCheckReportingUseCase> logger, IServiceScopeFactory scopeFactory)
    {
        _eligibilityCheckReportingGateway = eligibilityCheckReportingGateway;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<EligibilityCheckReportResponse> Execute(EligibilityCheckReportRequest model, CheckMetaData meta)
    {
        if (model == null) throw new ValidationException("Invalid request, model is required");

        var validator = new EligibilityCheckReportRequestValidator();
        var validationResults = validator.Validate(model);

        if (!validationResults.IsValid) throw new FluentValidation.ValidationException(validationResults.ToString());

        // create the report request
        var reportRequest = await _eligibilityCheckReportingGateway.CreateReport(model, CancellationToken.None);

        // generate the report in the background
        _ = Task.Run(async () =>
        {
            
            using var scope = _scopeFactory.CreateScope();
            var gateway = scope.ServiceProvider.GetRequiredService<IEligibilityCheckReporting>();
            try
            {
                await gateway.EligibilityCheckReports(reportRequest.EligibilityCheckReportId, model.EligibilityCheckType,null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background report generation failed for report {ReportId}", reportRequest.EligibilityCheckReportId.ToString());
            }
        });

        // all new reports will start with a status of 'New', so we can return that immediately without waiting for the report generation to complete
        return new EligibilityCheckReportResponse
        {
            Data = new EligibilityCheckReportResponseItem
            {
                ReportID = reportRequest.EligibilityCheckReportId.ToString(),
                Status = reportRequest.Status.ToString()
            }
        };

        
    }
}

