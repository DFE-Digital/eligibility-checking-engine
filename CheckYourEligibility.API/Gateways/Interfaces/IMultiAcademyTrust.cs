using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

/// <summary>
/// Provides access to Multi Academy Trust data and settings.
/// </summary>
public interface IMultiAcademyTrust
{
    /// <summary>
    /// Retrieves a Multi Academy Trust by its unique identifier.
    /// </summary>
    /// <param name="multiAcademyTrustId">The unique identifier of the Multi Academy Trust.</param>
    /// <returns>The matching <see cref="MultiAcademyTrust"/> if found; otherwise, null.</returns>
    Task<MultiAcademyTrust?> GetMultiAcademyTrustById(int multiAcademyTrustId);

    /// <summary>
    /// Updates the flag indicating whether academies under the specified Multi Academy Trust
    /// are allowed to review evidence.
    /// </summary>
    /// <param name="multiAcademyTrustId">The unique identifier of the Multi Academy Trust.</param>
    /// <param name="value">The value to set for the AcademyCanReviewEvidence flag.</param>
    /// <returns>The updated <see cref="MultiAcademyTrust"/> if successful; otherwise, null.</returns>
    Task<MultiAcademyTrust?> UpdateAcademyCanReviewEvidence(int multiAcademyTrustId, bool value);
}