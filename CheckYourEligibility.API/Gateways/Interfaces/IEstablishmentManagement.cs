using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces
{
    public interface IEstablishmentManagement
    {
        Task<SupportEstablishmentSearchResponse> Search(
            SupportEstablishmentSearchRequest request);

        Task<SupportEstablishmentResponse?> Get(
            int establishmentId);

        Task<bool> UpdatePrivateBeta(
            int establishmentId,
            bool inPrivateBeta);
    }
}