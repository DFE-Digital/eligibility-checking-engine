// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IEstablishmentSearch
{
    Task<IEnumerable<Establishment>?> Search(string query, string? la, string? mat);
}