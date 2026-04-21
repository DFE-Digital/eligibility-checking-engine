using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

/// <summary>
/// Provides data access and update operations for Multi Academy Trusts.
/// </summary>
public class MultiAcademyTrustGateway : IMultiAcademyTrust
{
    private readonly IEligibilityCheckContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiAcademyTrustGateway"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public MultiAcademyTrustGateway(IEligibilityCheckContext dbContext)
    {
        _db = dbContext;
    }

    /// <summary>
    /// Retrieves a Multi Academy Trust by its unique identifier.
    /// </summary>
    /// <param name="multiAcademyTrustId">The unique identifier of the Multi Academy Trust.</param>
    /// <returns>The matching <see cref="MultiAcademyTrust"/> if found; otherwise, null.</returns>
    public Task<MultiAcademyTrust?> GetMultiAcademyTrustById(int multiAcademyTrustId)
    {
        return _db.MultiAcademyTrusts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MultiAcademyTrustID == multiAcademyTrustId);
    }

    /// <summary>
    /// Updates the flag indicating whether academies under the specified Multi Academy Trust
    /// are allowed to review evidence.
    /// </summary>
    /// <param name="multiAcademyTrustId">The unique identifier of the Multi Academy Trust.</param>
    /// <param name="value">The value to set for the AcademyCanReviewEvidence flag.</param>
    /// <returns>The updated <see cref="MultiAcademyTrust"/> if successful; otherwise, null.</returns>
    public async Task<MultiAcademyTrust?> UpdateAcademyCanReviewEvidence(int multiAcademyTrustId, bool value)
    {
        var mat = await _db.MultiAcademyTrusts
            .FirstOrDefaultAsync(x => x.MultiAcademyTrustID == multiAcademyTrustId);

        if (mat == null)
            return null;

        mat.AcademyCanReviewEvidence = value;

        await _db.SaveChangesAsync();

        return mat;
    }
}