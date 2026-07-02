using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Requests;
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
        throw new NotImplementedException();
    }
}