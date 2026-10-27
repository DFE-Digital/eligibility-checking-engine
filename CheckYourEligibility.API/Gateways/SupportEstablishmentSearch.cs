using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using F23.StringSimilarity;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class SupportEstablishmentSearchGateway
    : ISupportEstablishmentSearch
{
    private readonly IEligibilityCheckContext _db;

    public SupportEstablishmentSearchGateway(
        IEligibilityCheckContext db)
    {
        _db = db;
    }

    public async Task<SupportEstablishmentSearchResponse> Search(
        SupportEstablishmentSearchRequest model)
    {
        IQueryable<Domain.Establishment> query = _db.Establishments
            .AsNoTracking()
            .Include(x => x.LocalAuthority);

        if (model.Urn.HasValue)
        {
            query = query.Where(x =>
                x.EstablishmentID == model.Urn.Value);
        }

        if (!string.IsNullOrWhiteSpace(model.Type))
        {
            query = query.Where(x =>
                x.Type.Contains(model.Type));
        }

        var queryResults = new List<Domain.Establishment>();

        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            query = query.Where(x =>
                x.EstablishmentName.Contains(model.Name));

            var establishments = await query.ToListAsync();

            var lev = new NormalizedLevenshtein();

            foreach (var establishment in establishments)
            {
                var distance = lev.Distance(
                    model.Name.ToUpper(),
                    establishment.EstablishmentName.ToUpper());

                establishment.LevenshteinDistance = distance;

                queryResults.Add(establishment);
            }

            queryResults = queryResults
                .Where(x =>
                    x.EstablishmentName.ToUpper()
                        .Contains(model.Name.ToUpper()))
                .OrderBy(x => x.LevenshteinDistance)
                .ThenBy(x => x.EstablishmentName)
                .ToList();
        }
        else
        {
            queryResults = await query
                .OrderBy(x => x.EstablishmentName)
                .ToListAsync();
        }

        var totalRecords = queryResults.Count;

        var totalPages = (int)Math.Ceiling(
            (double)totalRecords / model.PageSize);

        var pageNumber = model.PageNumber <= 0
            ? 1
            : model.PageNumber;

        var pagedResults = queryResults
            .Skip((pageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(x => new Establishment
            {
                Id = x.EstablishmentID,
                Name = x.EstablishmentName,
                Postcode = x.Postcode,
                Street = x.Street,
                Locality = x.Locality,
                Town = x.Town,
                County = x.County,
                La = x.LocalAuthority.LaName,
                Distance = x.LevenshteinDistance,
                Type = x.Type,
                InPrivateBeta = x.InPrivateBeta
            })
            .ToList();

        return new SupportEstablishmentSearchResponse
        {
            PageNumber = pageNumber,
            PageSize = model.PageSize,
            TotalNumberOfRecords = totalRecords,
            Data = pagedResults
        };
    }
}