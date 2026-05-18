using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class LocalAuthorityGateway : ILocalAuthority
{
    private readonly IEligibilityCheckContext _db;

    public LocalAuthorityGateway(IEligibilityCheckContext dbContext)
    {
        _db = dbContext;
    }

    public async Task<LocalAuthority?> GetLocalAuthorityById(int localAuthorityId)
    {
        return await _db.LocalAuthorities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LocalAuthorityID == localAuthorityId);
    }

    public async Task<LocalAuthority?> UpdateSchoolCanReviewEvidence(int localAuthorityId, bool value)
    {
        var la = await _db.LocalAuthorities
            .FirstOrDefaultAsync(x => x.LocalAuthorityID == localAuthorityId);

        if (la == null)
            return null;

        la.SchoolCanReviewEvidence = value;

        await _db.SaveChangesAsync();

        return la;
    }
    /// <summary>
    /// Get the Policy Id to be applied when checking benefits
    /// </summary>
    /// <param name="localAuthorityId"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public async Task<int?> GetEligibilityPolicyIdForTypeAsync(int localAuthorityId, CheckEligibilityType type)
    {

        var la = await GetLocalAuthorityById(localAuthorityId);

        switch (type)
        {

            case CheckEligibilityType.FreeSchoolMeals:
                return la.FreeSchoolMealsPolicyID;
            case CheckEligibilityType.EarlyYearPupilPremium:
                return la.EarlyYearsPupilPremiumPolicyID;
            case CheckEligibilityType.TwoYearOffer:
                return la.TwoYearPolicyID;
        };

        return null;

    }
}
