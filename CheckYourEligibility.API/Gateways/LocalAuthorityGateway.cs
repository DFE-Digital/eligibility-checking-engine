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

    public Task<LocalAuthority?> GetLocalAuthority(int localAuthorityId)
    {
        return _db.LocalAuthorities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LocalAuthorityID == localAuthorityId);
    }
}
