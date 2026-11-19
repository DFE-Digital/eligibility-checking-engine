using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

/// <summary>
///     Interface for retrieving working family events by eligibility code
/// </summary>
public interface IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="eligibilityCode"></param>
    /// <returns>returns a list of WorkingFamilyEventByEligibilityCodeResponseItems </returns>
    Task<WorkingFamilyEventByEligibilityCodeResponse> Execute(string eligibilityCode);
}


public class GetAllWorkingFamiliesEventsByEligibilityCodeUseCase : IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
{
    private readonly IWorkingFamiliesReporting _workingFamiliesReportingGateway;
    private readonly ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase> _logger;

    public GetAllWorkingFamiliesEventsByEligibilityCodeUseCase(IWorkingFamiliesReporting workingFamiliesReportingGateway, ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase> logger)
    {
        _workingFamiliesReportingGateway = workingFamiliesReportingGateway;
        _logger = logger;
    }

    public async Task<WorkingFamilyEventByEligibilityCodeResponse> Execute(string eligibilityCode)
    {
        if (string.IsNullOrEmpty(eligibilityCode))
            throw new ValidationException(null, "Invalid Request, Eligibility Code is required.");

        var response = await _workingFamiliesReportingGateway.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode);
        if (response == null)
        {
            _logger.LogWarning(
                $"Eligibilty Code {eligibilityCode.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} no events found");
            throw new NotFoundException(eligibilityCode);
        }

        _logger.LogInformation(
            $"Retrieved working family records for eligibility code: {eligibilityCode.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

        return new WorkingFamilyEventByEligibilityCodeResponse
        {
            Data = response.Data
        };
    }
}
