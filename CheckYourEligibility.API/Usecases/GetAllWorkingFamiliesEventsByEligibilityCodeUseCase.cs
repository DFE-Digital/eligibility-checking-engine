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
    Task<WorkingFamilyEventByEligibilityCodeRepsonse> Execute(string eligibilityCode, IList<int> allowedLocalAuthorityIds);
}


public class GetAllWorkingFamiliesEventsByEligibilityCodeUseCase : IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
{
    private readonly IWorkingFamiliesReporting _workingFamiliesReportingGateway;
    private readonly IAudit _auditGateway;
    private readonly ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase> _logger;

    public GetAllWorkingFamiliesEventsByEligibilityCodeUseCase(IWorkingFamiliesReporting workingFamiliesReportingGateway, IAudit auditGateway, ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase> logger)
    {
        _workingFamiliesReportingGateway = workingFamiliesReportingGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<WorkingFamilyEventByEligibilityCodeRepsonse> Execute(string eligibilityCode, IList<int> allowedLocalAuthorityIds)
    {
        if (string.IsNullOrEmpty(eligibilityCode))
            throw new ValidationException(null, "Invalid Request, Eligibility Code is required.");

        if (allowedLocalAuthorityIds == null || allowedLocalAuthorityIds.Count == 0)
        {
            throw new UnauthorizedAccessException("You do not have permission to access working families reporting");
        }

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
