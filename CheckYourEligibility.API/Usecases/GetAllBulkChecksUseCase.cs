using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving all bulk checks
/// </summary>
public interface IGetAllBulkChecksUseCase
{
    /// <summary>
    ///     Execute the use case to get all bulk checks
    /// </summary>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs for the user</param>
    /// <returns>All bulk checks the user has access to</returns>
    Task<CheckEligibilityBulkStatusesResponse> Execute(IList<int> allowedLocalAuthorityIds);
}

public class GetAllBulkChecksUseCase : IGetAllBulkChecksUseCase
{
    private readonly IBulkCheck _bulkCheckGateway;
    private readonly ILogger<GetAllBulkChecksUseCase> _logger;

    public GetAllBulkChecksUseCase(
        IBulkCheck bulkCheckGateway,
        ILogger<GetAllBulkChecksUseCase> logger)
    {
        _bulkCheckGateway = bulkCheckGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityBulkStatusesResponse> Execute(IList<int> allowedLocalAuthorityIds)
    {
        if (allowedLocalAuthorityIds == null || allowedLocalAuthorityIds.Count == 0)
        {
            throw new UnauthorizedAccessException("You do not have permission to access bulk checks");
        }

        IEnumerable<BulkCheck>? response;

        if (allowedLocalAuthorityIds.Contains(0))
        {
            // Admin user - get all non-deleted bulk checks
            _logger.LogInformation("Admin user retrieving all bulk checks");
            response = await GetAllBulkChecksForAdmin();
        }
        else
        {
            // Regular user - get bulk checks for their allowed local authorities
            _logger.LogInformation($"User retrieving bulk checks for local authorities: {string.Join(",", allowedLocalAuthorityIds)}");
            response = await GetBulkChecksForLocalAuthorities(allowedLocalAuthorityIds);
        }

        if (response == null || !response.Any())
        {
            _logger.LogInformation("No bulk checks found for the user's permissions");
            return new CheckEligibilityBulkStatusesResponse
            {
                Checks = Enumerable.Empty<Boundary.Responses.BulkCheck>()
            };
        }

        _logger.LogInformation($"Retrieved {response.Count()} bulk checks");

        return new CheckEligibilityBulkStatusesResponse
        {
            Checks = response.Select(bc => new Boundary.Responses.BulkCheck
            {
                Id = bc.BulkCheckID,
                SubmittedDate = bc.SubmittedDate,
                EligibilityType = bc.EligibilityType.ToString(),
                Status = bc.Status.ToString(),
                Filename = bc.Filename,
                SubmittedBy = bc.SubmittedBy,
                Get_BulkCheck_Results = $"/bulk-check/{bc.BulkCheckID}"
            }).OrderByDescending(bc => bc.SubmittedDate)
        };
    }

    private async Task<IEnumerable<BulkCheck>?> GetAllBulkChecksForAdmin()
    {
        // For admin users, we need to get all bulk checks across all local authorities
        // We can use a dummy local authority ID since admin permissions (0 in allowedLocalAuthorityIds) 
        // will override the filtering in the gateway
        // Pass false to get all bulk checks, not just from last 7 days
        return await _bulkCheckGateway.GetBulkStatuses("0", new List<int> { 0 }, includeLast7DaysOnly: false);
    }

    private async Task<IEnumerable<BulkCheck>?> GetBulkChecksForLocalAuthorities(IList<int> allowedLocalAuthorityIds)
    {
        var allBulkChecks = new List<BulkCheck>();

        // Get bulk checks for each allowed local authority
        // Pass false to get all bulk checks, not just from last 7 days
        foreach (var localAuthorityId in allowedLocalAuthorityIds)
        {
            var bulkChecks = await _bulkCheckGateway.GetBulkStatuses(localAuthorityId.ToString(), allowedLocalAuthorityIds, includeLast7DaysOnly: false);
            if (bulkChecks != null)
            {
                allBulkChecks.AddRange(bulkChecks);
            }
        }

        // Remove duplicates and return
        return allBulkChecks.GroupBy(bc => bc.BulkCheckID).Select(g => g.First());
    }
}