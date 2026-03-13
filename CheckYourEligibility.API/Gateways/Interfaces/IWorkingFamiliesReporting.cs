public interface IWorkingFamiliesReporting
{
    /// <summary>
    /// Gets events by eligiblity code
    /// </summary>
    /// <param name="eligibilityCode">Eligibility Code</param>
    /// <returns>WorkingFamilyEventByEligibilityCodeRepsonse response or null if not found</returns>
    Task<WorkingFamilyEventByEligibilityCodeRepsonse> GetAllWorkingFamiliesEventsByEligibilityCode(string eligibilityCode);
}