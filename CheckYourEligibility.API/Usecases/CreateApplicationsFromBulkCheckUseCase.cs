using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Gateways;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateApplicationsFromBulkCheckUseCase
{
    Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds);
    Task ProcessApplications(string bulkCheckId, List<int> allowedLocalAuthorityIds);
}

public class CreateApplicationsFromBulkCheckUseCase : ICreateApplicationsFromBulkCheckUseCase
{
    private readonly IDbContextFactory<EligibilityCheckContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CreateApplicationsFromBulkCheckUseCase> _logger;
    private readonly ICreateApplicationUseCase _createApplicationUseCase;

    public CreateApplicationsFromBulkCheckUseCase(
    IDbContextFactory<EligibilityCheckContext> dbContextFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<CreateApplicationsFromBulkCheckUseCase> logger,
    ICreateApplicationUseCase createApplicationUseCase)
    {
        _dbContextFactory = dbContextFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _createApplicationUseCase = createApplicationUseCase;
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
            [
                new Error
                {
                    Title = "Applications can only be created when bulk check status is 'Completed'"
                }
            ],
            "Invalid bulk check status");
        }

        var hasEligibleChecks = await dbContext.CheckEligibilities
        .AnyAsync(x =>
            x.BulkCheckID == bulkCheckId &&
            x.Status == CheckEligibilityStatus.eligible &&
            !x.IsDeleted);

        if (!hasEligibleChecks)
        {
            throw new ValidationException(
            [
                new Error
        {
            Title = "No eligible checks found for this bulk check"
        }
            ],
            "No eligible checks found");
        }

        bulkCheck.Status = BulkCheckStatus.ApplicationCreationInProgress;

        await dbContext.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();

            var scopedUseCase =
                scope.ServiceProvider.GetRequiredService<ICreateApplicationsFromBulkCheckUseCase>();

            try
            {
                await scopedUseCase.ProcessApplications(
                    bulkCheckId,
                    allowedLocalAuthorityIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application creation failed for bulk check {BulkCheckId}", bulkCheckId);
            }
        });

        return new MessageResponse
        {
            Data = "Application creation started."
        };
    }

    public async Task ProcessApplications(
        string bulkCheckId,
        List<int> allowedLocalAuthorityIds)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var eligibleChecks = await dbContext.CheckEligibilities
            .Where(x =>
                x.BulkCheckID == bulkCheckId &&
                x.Status == CheckEligibilityStatus.eligible &&
                !x.IsDeleted)
            .ToListAsync();

        var hasFailures = false;

        foreach (var check in eligibleChecks)
        {
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(check.CheckData);

            if (checkData == null)
            {
                _logger.LogWarning(
                    "Skipping eligibility check {EligibilityCheckId} because CheckData could not be deserialised",
                    check.EligibilityCheckID);

                continue;
            }

            if (!int.TryParse(checkData.ChildSchoolURN, out var establishment))
            {
                _logger.LogWarning(
                    "Skipping eligibility check {EligibilityCheckId} because ChildSchoolURN is missing or invalid",
                    check.EligibilityCheckID);

                continue;
            }

            var applicationRequest = new ApplicationRequest
            {
                Data = new ApplicationRequestData
                {
                    Type = CheckEligibilityType.FreeSchoolMeals,
                    Establishment = establishment,
                    ParentFirstName = checkData.FirstName!,
                    ParentLastName = checkData.LastName!,
                    ParentEmail = string.Empty,
                    ParentNationalInsuranceNumber = checkData.NationalInsuranceNumber,
                    ParentNationalAsylumSeekerServiceNumber = checkData.NationalAsylumSeekerServiceNumber,
                    ParentDateOfBirth = checkData.DateOfBirth,
                    ChildFirstName = checkData.ChildFirstName!,
                    ChildLastName = checkData.ChildLastName!,
                    ChildDateOfBirth = checkData.ChildDateOfBirth!,
                    UserId = null,
                    Evidence = []
                }
            };

            try
            {
                await _createApplicationUseCase.Execute(applicationRequest, allowedLocalAuthorityIds);
            }
            catch (Exception ex)
            {
                hasFailures = true;

                _logger.LogError(
                    ex,
                    "Application creation failed for eligibility check {EligibilityCheckId}",
                    check.EligibilityCheckID);
            }
        }

        var bulkCheck = await dbContext.BulkChecks
            .FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId);

        if (bulkCheck != null)
        {
            bulkCheck.Status = hasFailures
                ? BulkCheckStatus.ApplicationCreationFailed
                : BulkCheckStatus.ApplicationsCreated;

            await dbContext.SaveChangesAsync();
        }
    }
}