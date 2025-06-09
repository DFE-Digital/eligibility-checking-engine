using System;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
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
    Task<CheckEligibilityBulkStatusesResponse> Execute(string guid);
}

public class GetBulkCheckStatusesUseCase : IGetBulkCheckStatusesUseCase
{
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<GetBulkCheckStatusesUseCase> _logger;

    public GetBulkCheckStatusesUseCase(
        ICheckEligibility checkGateway,
        ILogger<GetBulkCheckStatusesUseCase> logger)
    {
        _checkGateway = checkGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityBulkStatusesResponse> Execute(string localAuthority)
    {
        if (string.IsNullOrEmpty(localAuthority)) throw new ValidationException(null, "Invalid Request, localAuthority is required.");

        var response = await _checkGateway.GetBulkStatuses(localAuthority);
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
            Checks = response
        };
    }
}