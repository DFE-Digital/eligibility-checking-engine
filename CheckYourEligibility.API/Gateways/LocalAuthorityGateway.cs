using CheckYourEligibility.API.Domain;
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

    public Task<LocalAuthority?> GetLocalAuthorityById(int localAuthorityId)
    {
        return _db.LocalAuthorities
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
}
