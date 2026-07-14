using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

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
        var bulkCheck = await _bulkCheckGateway.GetBulkCheck(bulkCheckId.ToString());

        if (bulkCheck == null)
        {
            throw new NotFoundException();
        }

        if (!allowedLocalAuthorityIds.Contains(0) &&
            (bulkCheck.LocalAuthorityID == null ||
             !allowedLocalAuthorityIds.Contains(bulkCheck.LocalAuthorityID.Value)))
        {
            _logger.LogWarning(
                $"User attempted to access bulk check {bulkCheckId} belonging to local authority {bulkCheck.LocalAuthorityID} without permission");

            throw new UnauthorizedAccessException(
                $"You do not have permission to access bulk check {bulkCheckId}");
        }

        var results = await _bulkCheckGateway
            .GetBulkCheckResults<IList<CheckEligibilityItem>>(bulkCheckId.ToString());

        var outcomes = results
            .GroupBy(result =>
                string.IsNullOrWhiteSpace(result.Tier)
                    ? result.Status
                    : $"{result.Status}-{result.Tier}".ToLower())
            .ToDictionary(group => group.Key, group => group.Count());

        return new BulkCheckSummaryResponse
        {
            Filename = bulkCheck.Filename,
            Status = bulkCheck.Status.ToString(),
            SubmittedDate = bulkCheck.SubmittedDate,
            SubmittedBy = bulkCheck.SubmittedBy,
            Outcomes = outcomes
        };
    }
}