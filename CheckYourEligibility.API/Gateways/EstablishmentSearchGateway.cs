using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using F23.StringSimilarity;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class EstablishmentSearchGateway : IEstablishmentSearch
{
    private const int takeScoolResultsMax = 20;
    private readonly IEligibilityCheckContext _db;

    private readonly ILogger _logger;

    public EstablishmentSearchGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext)
    {
        _logger = logger.CreateLogger("EstablishmentSearchService");
        _db = dbContext;
    }

    [ExcludeFromCodeCoverage(Justification = "memory only db breaks test in full run, works fine run locally")]
    public async Task<IEnumerable<Establishment>?> Search(string query, string? la, string? mat)
    {
        var results = new List<Establishment>();

        int.TryParse(la, out int laInt);
        int.TryParse(mat, out int matInt);
        var matSchools = mat != null ? _db.MultiAcademyTrustEstablishments.Where(x => x.MultiAcademyTrustID == matInt).Select(x => x.EstablishmentID).ToList() : null;

        if (int.TryParse(query, out var EstablishmentId))
        {
            var establishmentFromUrn = _db.Establishments
                .Include(x => x.LocalAuthority)
                .FirstOrDefault(x => x.StatusOpen &&
                                     x.EstablishmentID == EstablishmentId &&
                                     (la == null || x.LocalAuthorityID.Equals(laInt)) &&
                                     (matSchools == null || matSchools.Contains(x.EstablishmentID)));

            if (establishmentFromUrn != null)
            {
                var item = new Establishment
                {
                    Id = establishmentFromUrn.EstablishmentID,
                    Name = establishmentFromUrn.EstablishmentName,
                    Postcode = establishmentFromUrn.Postcode,
                    Locality = establishmentFromUrn.Locality,
                    County = establishmentFromUrn.County,
                    Street = establishmentFromUrn.Street,
                    Town = establishmentFromUrn.Town,
                    La = establishmentFromUrn.LocalAuthority.LaName,
                    Type = establishmentFromUrn.Type
                };
                results.Add(item);
            }

            return results;
        }

        var allEstablishments = _db.Establishments
            .Where(x => x.StatusOpen &&
                        x.EstablishmentName.Contains(query) &&
                        (la == null || x.LocalAuthorityID.Equals(laInt)) &&
                        (matSchools == null || matSchools.Contains(x.EstablishmentID)))
            .Include(x => x.LocalAuthority);

        var queryResult = new List<Domain.Establishment>();

        var lev = new NormalizedLevenshtein();
        double levenshteinDistance;
        foreach (var item in allEstablishments)
        {
            levenshteinDistance =
                lev.Distance(query.ToUpper(),
                    item.EstablishmentName.ToUpper()); // lev.DistanceFrom(item.EstablishmentName);
            if (levenshteinDistance < 20)
            {
                item.LevenshteinDistance = levenshteinDistance;
                queryResult.Add(item);
            }
        }


        return queryResult.Where(x => x.EstablishmentName.ToUpper().Contains(query.ToUpper()))
            .OrderBy(x => x.LevenshteinDistance)
            .ThenBy(x => x.EstablishmentName).Take(takeScoolResultsMax)
            .Select(x => new Establishment
            {
                Id = x.EstablishmentID,
                Name = x.EstablishmentName,
                Postcode = x.Postcode,
                Locality = x.Locality,
                County = x.County,
                Street = x.Street,
                Town = x.Town,
                La = x.LocalAuthority.LaName,
                Distance = x.LevenshteinDistance,
                Type = x.Type
            });
    }
}