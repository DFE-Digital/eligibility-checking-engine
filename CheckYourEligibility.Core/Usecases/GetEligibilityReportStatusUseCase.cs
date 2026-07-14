using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Exceptions;

public interface IGetEligibilityReportStatusUseCase
{
    Task<EligibilityReportStatusResponse> Execute(string reportId);
}

public class GetEligibilityReportStatusUseCase : IGetEligibilityReportStatusUseCase
{
    private readonly IEligibilityCheckReporting _eligibilityCheckReportingGateway;
    private readonly ILogger<GetEligibilityReportStatusUseCase> _logger;

    public GetEligibilityReportStatusUseCase(IEligibilityCheckReporting eligibilityCheckReportingGateway, ILogger<GetEligibilityReportStatusUseCase> logger)
    {
        _eligibilityCheckReportingGateway = eligibilityCheckReportingGateway;
        _logger = logger;
    }

    public async Task<EligibilityReportStatusResponse> Execute(string reportId)
    {

        if (!Guid.TryParse(reportId, out Guid id)) {
            
            throw new ValidationException(null, "Invalid report ID format. Must be a GUID");

        }

        var response = await _eligibilityCheckReportingGateway.GetEligibilityReportById(id);

        if (response == null) {

            _logger.LogWarning(
                "Eligibility check report with ID {ReportId} not found", id);
            throw new NotFoundException();
        }        
        
            return new EligibilityReportStatusResponse
            {
                Status = response.Status == null ? ReportStatus.Archived.ToString() : response.Status.ToString()
            };
    }
}