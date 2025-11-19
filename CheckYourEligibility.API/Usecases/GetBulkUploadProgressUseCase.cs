using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving bulk upload progress status
/// </summary>
public interface IGetBulkUploadProgressUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The group ID of the bulk upload</param>
    /// <returns>Bulk upload progress status</returns>
    Task<CheckEligibilityBulkStatusResponse> Execute(string guid);
}

public class GetBulkUploadProgressUseCase : IGetBulkUploadProgressUseCase
{
    private readonly IBulkCheck _bulkCheckGateway;
    private readonly ILogger<GetBulkUploadProgressUseCase> _logger;

    public GetBulkUploadProgressUseCase(
        IBulkCheck bulkCheckGateway,
        ILogger<GetBulkUploadProgressUseCase> logger)
    {
        _bulkCheckGateway = bulkCheckGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityBulkStatusResponse> Execute(string guid)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, group ID is required.");

        var response = await _bulkCheckGateway.GetBulkStatus(guid);
        if (response == null)
        {
            _logger.LogWarning(
                $"Bulk upload with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
            throw new NotFoundException(guid);
        }

        _logger.LogInformation(
            $"Retrieved bulk upload progress for group ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

        return new CheckEligibilityBulkStatusResponse
        {
            Data = response,
            Links = new BulkCheckResponseLinks
            {
                Get_BulkCheck_Results = $"{CheckLinks.BulkCheckLink}{guid}{CheckLinks.BulkCheckResults}"
            }
        };
    }
}