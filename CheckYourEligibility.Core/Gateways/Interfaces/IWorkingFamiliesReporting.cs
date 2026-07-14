
using CheckYourEligibility.Core.Boundary.Responses;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IWorkingFamiliesReporting
{
    /// <summary>
    /// Gets events by eligiblity code
    /// </summary>
    /// <param name="eligibilityCode">Eligibility Code</param>
    /// <returns>WorkingFamilyEventByEligibilityCodeResponse response or null if not found</returns>
    Task<WorkingFamilyEventByEligibilityCodeResponse> GetAllWorkingFamiliesEventsByEligibilityCode(string eligibilityCode);
}