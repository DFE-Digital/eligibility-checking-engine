using CheckYourEligibility.Core.Boundary.Responses;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IEstablishmentSearch
{
    Task<IEnumerable<Establishment>?> Search(string query, string? la, string? mat);

    Task<int?> GetEstablishmentLAIdAsync(int establishmentId);
}