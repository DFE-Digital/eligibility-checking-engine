using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

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

    Task<WorkingFamiliesCorrectionRecord?> UpdatePartner(
        string eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest request,
        CancellationToken cancellationToken = default);
}