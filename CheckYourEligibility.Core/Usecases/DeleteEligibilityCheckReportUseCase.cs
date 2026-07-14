
namespace CheckYourEligibility.Core.UseCases;

/// <summary>
/// Interface for the delete eligibility check report use case
/// </summary>
public interface IDeleteEligibilityCheckReportUseCase
{
    /// <summary>
    /// Deletes an eligibility check report after validating local authority permissions
    /// </summary>
    /// <param name="reportId"></param>
    /// <param name="localAuthorityIds"></param>
    /// <returns></returns>
    Task Execute(Guid reportId, List<int> localAuthorityIds);
}

/// <summary>
/// Implementation of the delete eligibility check report use case
/// </summary>
public class DeleteEligibilityCheckReportUseCase : IDeleteEligibilityCheckReportUseCase
{
    private readonly IEligibilityCheckReporting _gateway;
    private readonly ILogger<DeleteEligibilityCheckReportUseCase> _logger;

    /// <summary>
    /// Constructor for DeleteEligibilityCheckReportUseCase
    /// </summary>
    /// <param name="gateway">The eligibility check reporting gateway</param>
    /// <param name="logger">The logger</param>
    public DeleteEligibilityCheckReportUseCase(
        IEligibilityCheckReporting gateway,
        ILogger<DeleteEligibilityCheckReportUseCase> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    /// <summary>
    /// Deletes an eligibility check report after validating local authority permissions.
    /// </summary>
    /// <param name="reportId">The ID of the report to delete</param>
    /// <param name="localAuthorityIds">The list of local authority IDs the user has access to</param>
    /// <returns>Task</returns>
    public async Task Execute(Guid reportId, List<int> localAuthorityIds)
    {
        // Validate parameters
        if (reportId == Guid.Empty)
            throw new ArgumentNullException(nameof(reportId));

        // localAuthorityIds can be empty, but not null. If empty, user has no permissions and will be blocked below.
        if (localAuthorityIds == null)
            throw new ArgumentNullException(nameof(localAuthorityIds));

        // If user has no permissions at all, block immediately
        if (localAuthorityIds.Count == 0)
            throw new UnauthorizedAccessException("You do not have permission to delete reports for this local authority");

        // Check if the report exists and get its local authority ID
        var reportLocalAuthorityId = await _gateway.GetLocalAuthorityIdForReport(reportId, CancellationToken.None);

        // Check if user has permission: either has "all" permissions (0) or their LA matches the report's LA
        if (!localAuthorityIds.Contains(0) && !localAuthorityIds.Contains(reportLocalAuthorityId))
            throw new UnauthorizedAccessException("You do not have permission to delete reports for this local authority");

        await _gateway.DeleteEligibilityCheckReport(reportId, CancellationToken.None);
        _logger.LogInformation("Soft deleted eligibility report {ReportId}", reportId);
    }
}
