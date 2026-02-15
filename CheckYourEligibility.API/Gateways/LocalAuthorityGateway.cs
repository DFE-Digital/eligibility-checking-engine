using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class LocalAuthorityGateway : ILocalAuthority
{
    private readonly CheckYourEligibilityContext _context;

    public LocalAuthorityGateway(CheckYourEligibilityContext context)
    {
        _context = context;
    }

    public Task<LocalAuthority?> GetById(int localAuthorityId)
    {
        return _context.LocalAuthorities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LocalAuthorityID == localAuthorityId);
    }
}
