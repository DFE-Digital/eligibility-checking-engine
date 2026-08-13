using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IGetBulkCheckSummaryUseCase
{
    Task<BulkCheckSummaryResponse> Execute(
        Guid bulkCheckId,
        IList<int> allowedLocalAuthorityIds,
        CheckMetaData meta);
}

public class GetBulkCheckSummaryUseCase : IGetBulkCheckSummaryUseCase
{
    private readonly IBulkCheck _bulkCheckGateway;
    private readonly ILogger<GetBulkCheckSummaryUseCase> _logger;

    public GetBulkCheckSummaryUseCase(
        IBulkCheck bulkCheckGateway,
        ILogger<GetBulkCheckSummaryUseCase> logger)
    {
        _bulkCheckGateway = bulkCheckGateway;
        _logger = logger;
    }

    public async Task<BulkCheckSummaryResponse> Execute(
        Guid bulkCheckId,
        IList<int> allowedLocalAuthorityIds,
        CheckMetaData meta)
    {
        var bulkCheck = await _bulkCheckGateway.GetBulkCheck(bulkCheckId.ToString())
            ?? throw new NotFoundException();

        var hasAccess =
            allowedLocalAuthorityIds.Contains(0) ||
            (bulkCheck.LocalAuthorityID.HasValue &&
             allowedLocalAuthorityIds.Contains(bulkCheck.LocalAuthorityID.Value));

        if (!hasAccess)
        {
            _logger.LogWarning(
                "User attempted to access bulk check {BulkCheckId} belonging to local authority {LocalAuthorityId} without permission",
                bulkCheckId,
                bulkCheck.LocalAuthorityID);

            throw new UnauthorizedAccessException(
                $"You do not have permission to access bulk check {bulkCheckId}");
        }

        var results = await _bulkCheckGateway.GetBulkCheckResults(bulkCheckId.ToString());

        var outcomes = results
            .GroupBy(GetOutcomeKey)
            .ToDictionary(g => g.Key, g => g.Count());

        return new BulkCheckSummaryResponse
        {
            Filename = bulkCheck.Filename,
            Status = bulkCheck.Status.ToString(),
            SubmittedDate = bulkCheck.SubmittedDate,
            SubmittedBy = bulkCheck.SubmittedBy,
            Outcomes = outcomes
        };
    }

    private static string GetOutcomeKey(EligibilityCheck result)
    {
        return result.Tier == null
            ? result.Status.ToString().ToLowerInvariant()
            : $"{result.Status}-{result.Tier}".ToLowerInvariant();
    }
}