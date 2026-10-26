using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IWorkingFamiliesCorrection
{
    Task<WorkingFamiliesCorrectionRecord?> GetByEligibilityCode(
        string eligibilityCode,
        CancellationToken cancellationToken = default);
}