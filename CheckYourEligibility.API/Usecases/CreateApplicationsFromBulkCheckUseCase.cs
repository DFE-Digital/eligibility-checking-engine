using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateApplicationsFromBulkCheckUseCase
{
    Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds);
}

public class CreateApplicationsFromBulkCheckUseCase : ICreateApplicationsFromBulkCheckUseCase
{
    private readonly IDbContextFactory<EligibilityCheckContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public CreateApplicationsFromBulkCheckUseCase(
        IDbContextFactory<EligibilityCheckContext> dbContextFactory,
        IServiceScopeFactory scopeFactory)
    {
        _dbContextFactory = dbContextFactory;
        _scopeFactory = scopeFactory;
    }

    public async Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var bulkCheck = await dbContext.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId);

        if (bulkCheck == null)
        {
            throw new NotFoundException($"Bulk check {bulkCheckId} not found");
        }

        if (bulkCheck.Status != BulkCheckStatus.Completed)
        {
            throw new ValidationException(
                "Applications can only be created when bulk check status is 'Completed'");
        }

        bulkCheck.Status = BulkCheckStatus.ApplicationCreationInProgress;

        await dbContext.SaveChangesAsync();

        return new MessageResponse
        {
            Data = "Application creation started."
        };
    }
}