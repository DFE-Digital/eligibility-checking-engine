using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IWorkingFamiliesCorrection
{
    Task<WorkingFamiliesCorrectionRecord?> GetByEligibilityCode(
        string eligibilityCode,
        CancellationToken cancellationToken = default);

    Task<WorkingFamiliesCorrectionRecord?> UpdateCarer(
        string eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkingFamiliesCorrectionRecord?> UpdateChild(
        string eligibilityCode,
        WorkingFamiliesChildCorrectionRequest request,
        CancellationToken cancellationToken = default);
}