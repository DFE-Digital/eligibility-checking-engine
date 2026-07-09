using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using OrganisationType = CheckYourEligibility.API.Domain.Constants.OrganisationType;
using CheckYourEligibility.API.Gateways.Interfaces;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for retrieving all bulk checks
/// </summary>
public interface IGetAllBulkChecksUseCase
{
    /// <summary>
    ///     Execute the use case to get all bulk checks the user has access to,
    ///     based on their organisation scope and metadata.
    /// </summary>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs for the user.</param>
    /// <param name="meta">Metadata describing the user's organisation scope.</param>
    /// <returns>All bulk checks the user has access to.</returns>
    Task<CheckEligibilityBulkStatusesResponse> Execute(
        IList<int> allowedLocalAuthorityIds,
        CheckMetaData meta);
}

public class GetAllBulkChecksUseCase : IGetAllBulkChecksUseCase
{
    private readonly IBulkCheck _bulkCheckGateway;
    private readonly IMultiAcademyTrust _multiAcademyTrustGateway;
    private readonly ILogger<GetAllBulkChecksUseCase> _logger;

    public GetAllBulkChecksUseCase(
        IBulkCheck bulkCheckGateway,
        IMultiAcademyTrust multiAcademyTrustGateway,
        ILogger<GetAllBulkChecksUseCase> logger)
    {
        _bulkCheckGateway = bulkCheckGateway;
        _multiAcademyTrustGateway = multiAcademyTrustGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityBulkStatusesResponse> Execute(
        IList<int> allowedLocalAuthorityIds,
        CheckMetaData meta)
    {

        IEnumerable<BulkCheck>? response = [];


        if (meta.OrganisationID != 0 && meta.OrganisationType == OrganisationType.multi_academy_trust)
        {
            response = await GetBulkChecksForMultiAcademyTrust(meta.OrganisationID ?? 0, meta.Source);
        }
        else if (meta.OrganisationID != 0 && meta.OrganisationType == OrganisationType.establishment)
        {
            response = await GetBulkChecksForEstablishment(meta.OrganisationID ?? 0, meta.Source);
        }
        else if (meta.OrganisationID != 0 && meta.OrganisationType == OrganisationType.local_authority)
        {
            response = await GetBulkChecksForLocalAuthority(meta.OrganisationID ?? 0, meta.Source);
        }
        else if (meta.OrganisationID == 0 && meta.OrganisationType == OrganisationType.local_authority)
        {
            response = await GetBulkChecksForLocalAuthorities(allowedLocalAuthorityIds, meta.Source);
        }
        else if (allowedLocalAuthorityIds.Contains(0))
        {
            response = await GetAllBulkChecksForAdmin();
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
        //TO DO: use map
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
                NumberOfRecords = bc.NumberOfRecords,
                FinalNameInCheck = bc.FinalNameInCheck,
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
        return await _bulkCheckGateway.GetBulkStatuses("0", new List<int> { 0 }, null, includeLast7DaysOnly: false);
    }

    private async Task<IEnumerable<BulkCheck>?> GetBulkChecksForLocalAuthorities(IList<int> allowedLocalAuthorityIds, string source)
    {
        var allBulkChecks = new List<BulkCheck>();

        // Get bulk checks for each allowed local authority
        // Pass true to get all bulk checks from last 7 days
        foreach (var localAuthorityId in allowedLocalAuthorityIds)
        {
            var bulkChecks = await _bulkCheckGateway.GetBulkStatuses(localAuthorityId.ToString(), allowedLocalAuthorityIds, source, includeLast7DaysOnly: true);
            if (bulkChecks != null)
            {
                allBulkChecks.AddRange(bulkChecks);
            }
        }

        // Remove duplicates and return
        return allBulkChecks.GroupBy(bc => bc.BulkCheckID).Select(g => g.First());
    }

    private async Task<IEnumerable<BulkCheck>?> GetBulkChecksForLocalAuthority(int localAuthorityId, string source)
    {
        return await GetBulkChecksForLocalAuthorities([localAuthorityId], source);
    }

    private async Task<IEnumerable<BulkCheck>?> GetBulkChecksForEstablishment(int establishmentId, string source)
    {
        return await _bulkCheckGateway.GetBulkChecksByOrganisation(OrganisationType.establishment, source, establishmentId);
    }

    private async Task<IEnumerable<BulkCheck>?> GetBulkChecksForMultiAcademyTrust(int multiAcademyTrustId, string source)
    {
        var establishmentIds = await _multiAcademyTrustGateway.GetEstablishmentIdsForMultiAcademyTrust(multiAcademyTrustId);
        var matBulkChecks = await _bulkCheckGateway.GetBulkChecksByOrganisation(OrganisationType.multi_academy_trust, source, multiAcademyTrustId);
        var establishmentBulkChecks = new List<BulkCheck>();

        foreach (var establishmentId in establishmentIds)
        {
            var checks = await _bulkCheckGateway.GetBulkChecksByOrganisation(OrganisationType.establishment, source, establishmentId);
            if (checks != null)
            {
                establishmentBulkChecks.AddRange(checks);
            }
        }

        return (matBulkChecks ?? Enumerable.Empty<BulkCheck>())
            .Concat(establishmentBulkChecks)
            .GroupBy(x => x.BulkCheckID)
            .Select(x => x.First());
    }
}