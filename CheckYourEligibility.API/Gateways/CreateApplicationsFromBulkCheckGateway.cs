using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class CreateApplicationsFromBulkCheckGateway : ICreateApplicationsFromBulkCheck
{
    private readonly IDbContextFactory<EligibilityCheckContext> _dbContextFactory;

    public CreateApplicationsFromBulkCheckGateway(
        IDbContextFactory<EligibilityCheckContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<BulkCheck?> GetBulkCheck(string bulkCheckId)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId);
    }

    public async Task<bool> HasEligibleChecks(string bulkCheckId)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.CheckEligibilities
            .AnyAsync(x =>
                x.BulkCheckID == bulkCheckId &&
                x.Status == CheckEligibilityStatus.eligible &&
                !x.IsDeleted);
    }

    public async Task<List<EligibilityCheck>> GetEligibleChecks(string bulkCheckId)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.CheckEligibilities
            .Where(x =>
                x.BulkCheckID == bulkCheckId &&
                x.Status == CheckEligibilityStatus.eligible &&
                !x.IsDeleted)
            .ToListAsync();
    }

    public async Task UpdateBulkCheckStatus(string bulkCheckId, BulkCheckStatus status)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var bulkCheck = await dbContext.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId);

        if (bulkCheck == null)
        {
            return;
        }

        bulkCheck.Status = status;

        await dbContext.SaveChangesAsync();
    }
}