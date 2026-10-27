using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ISupportEstablishmentSearch
{
    Task<SupportEstablishmentSearchResponse> Search(
        SupportEstablishmentSearchRequest request);
}