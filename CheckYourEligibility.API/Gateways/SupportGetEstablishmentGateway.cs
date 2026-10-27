using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

public class SupportGetEstablishmentGateway : ISupportGetEstablishment
{
    private readonly IEligibilityCheckContext _db;

    public SupportGetEstablishmentGateway(
        IEligibilityCheckContext db)
    {
        _db = db;
    }

    public async Task<SupportEstablishmentResponse?> Get(
        int establishmentId)
    {
        var establishment = await _db.Establishments
            .AsNoTracking()
            .Include(x => x.LocalAuthority)
            .FirstOrDefaultAsync(x =>
                x.EstablishmentID == establishmentId);

        if (establishment == null)
        {
            return null;
        }

        var mat = await _db.MultiAcademyTrustEstablishments
            .AsNoTracking()
            .Include(x => x.MultiAcademyTrust)
            .Where(x => x.EstablishmentID == establishmentId)
            .Select(x => x.MultiAcademyTrust)
            .FirstOrDefaultAsync();

        return new SupportEstablishmentResponse
        {
            Establishment = new Establishment
            {
                Id = establishment.EstablishmentID,
                Name = establishment.EstablishmentName,
                Postcode = establishment.Postcode,
                Street = establishment.Street,
                Locality = establishment.Locality,
                Town = establishment.Town,
                County = establishment.County,
                La = establishment.LocalAuthority.LaName,
                Type = establishment.Type,
                InPrivateBeta = establishment.InPrivateBeta
            },

            LocalAuthority = establishment.LocalAuthority,

            MultiAcademyTrust = mat
        };
    }
}