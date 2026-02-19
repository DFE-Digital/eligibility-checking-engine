using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving bulk upload progress status
/// </summary>
public interface IGetBulkCheckStatusesUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The group ID of the bulk upload</param>
    /// <returns>Bulk upload progress status</returns>
    Task<CheckEligibilityBulkStatusesResponse> Execute(string localAuthority, IList<int> allowedLocalAuthorityIds);
}

public class GetBulkCheckStatusesUseCase : IGetBulkCheckStatusesUseCase
{
    private readonly IBulkCheck _bulkCheckGateway;
    private readonly ILogger<GetBulkCheckStatusesUseCase> _logger;

    public GetBulkCheckStatusesUseCase(
        IBulkCheck bulkCheckGateway,
        ILogger<GetBulkCheckStatusesUseCase> logger)
    {
        _bulkCheckGateway = bulkCheckGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityBulkStatusesResponse> Execute(string localAuthority,
        IList<int> allowedLocalAuthorityIds)
    {
        if (string.IsNullOrEmpty(localAuthority))
            throw new ValidationException(null, "Invalid Request, localAuthority is required.");

        if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(int.Parse(localAuthority)))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to access applications for this establishment's local authority");
        }

        var response = await _bulkCheckGateway.GetBulkStatuses(localAuthority, allowedLocalAuthorityIds);
        if (response == null)
        {
            _logger.LogWarning(
                $"Bulk upload with ID {localAuthority.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
            throw new NotFoundException(localAuthority);
        }

        _logger.LogInformation(
            $"Retrieved bulk upload progress for local authority: {localAuthority.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");

        return new CheckEligibilityBulkStatusesResponse
        {
            Checks = response.Select(bc => new BulkCheck
            {
                Id = bc.BulkCheckID,
                SubmittedDate = bc.SubmittedDate,
                EligibilityType = bc.EligibilityType.ToString(),
                Status = bc.Status.ToString(),
                Filename = bc.Filename,
                FinalNameInCheck = bc.FinalNameInCheck,
                NumberOfRecords = bc.NumberOfRecords,
                SubmittedBy = bc.SubmittedBy,
                Get_BulkCheck_Results = $"/bulk-check/{bc.BulkCheckID}/results"
            })
        };
    }
}