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

    public GetAllWorkingFamiliesEventsByEligibilityCodeUseCase(IWorkingFamiliesReporting workingFamiliesReportingGateway, IAudit auditGateway)
    {
        _workingFamiliesReportingGateway = workingFamiliesReportingGateway;
        _auditGateway = auditGateway;
    }

    public Task<WorkingFamilyEventByEligibilityCodeRepsonse> Execute(string eligibilityCode, IList<int> allowedLocalAuthorityIds)
    {
        throw new NotImplementedException();
    }
}
