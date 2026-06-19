using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class LocalAuthorityGateway : ILocalAuthority
{
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public LocalAuthorityGateway(IEligibilityCheckContext dbContext, ILogger<LocalAuthorityGateway> logger)
    {
        _db = dbContext;
        _logger = logger;
    }

    public async Task<LocalAuthority?> GetLocalAuthorityById(int localAuthorityId, EligibilityCheckContext? dbContextFactory)
    {
        var context = dbContextFactory ?? _db;
        return await context.LocalAuthorities
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
    public async Task<int> GetEligibilityPolicyIdForTypeAsync(int localAuthorityId, CheckEligibilityType type, EligibilityCheckContext? dbContextFactory)
    {

        var la = await GetLocalAuthorityById(localAuthorityId, dbContextFactory);

        switch (type)
        {

            case CheckEligibilityType.FreeSchoolMeals:
                return la.FreeSchoolMealsPolicyID;
            case CheckEligibilityType.EarlyYearPupilPremium:
                return la.EarlyYearsPupilPremiumPolicyID;
            case CheckEligibilityType.TwoYearOffer:
                return la.TwoYearPolicyID;
        }
        ;

        return 0;

    }

    /// <summary>
    /// Get all establishments by local authority id
    /// </summary>
    /// <param name="localAuthorityId"></param>
    /// <returns></returns>
    public async Task<List<EstablishmentResponseItem>> GetEstablishmentsByLocalAuthorityId(int localAuthorityId)
    {
        try
        {

            var laExists = await _db.LocalAuthorities
                .AnyAsync(x => x.LocalAuthorityID == localAuthorityId);

            if (!laExists)
            {
                throw new NotFoundException($"Local authority: - {localAuthorityId}, is not found");
            }

            var result = await _db.Establishments
                .Where(x => x.LocalAuthorityID == localAuthorityId)
                .Select(e => new EstablishmentResponseItem
                {
                    URN = e.EstablishmentID,
                    Name = e.EstablishmentName
                })
                .AsNoTracking()
                .ToListAsync();

            return result;
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "Error retrieving establishments with Id: -", localAuthorityId);
            throw new NotFoundException($"Unable to find establishments: - {localAuthorityId}, {ex.Message}");
        }
    }

}

