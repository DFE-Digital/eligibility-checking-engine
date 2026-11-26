using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;

namespace CheckYourEligibility.Core.Gateways.Interfaces
{
    public interface IEstablishmentManagement
    {
        Task<SupportEstablishmentSearchResponse> Search(SupportEstablishmentSearchRequest request);

        Task<SupportEstablishmentResponse?> Get(int establishmentId);

        Task<bool> UpdatePrivateBeta(int establishmentId, bool inPrivateBeta);
    }
}