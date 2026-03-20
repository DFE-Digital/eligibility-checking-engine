using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving working family events by eligibility code
/// </summary>
public interface IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="eligibilityCode"></param>
    /// <returns>returns a list of WorkingFamilyEventByEligibilityCodeRepsonseItems </returns>
    Task<WorkingFamilyEventByEligibilityCodeRepsonse> Execute(string eligibilityCode);
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

    public async Task<WorkingFamilyEventByEligibilityCodeRepsonse> Execute(string eligibilityCode)
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

        return new WorkingFamilyEventByEligibilityCodeRepsonse
        {
            Data = response.Data
        };
    }
}
