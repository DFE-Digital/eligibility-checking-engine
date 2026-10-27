using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces
{
    public interface ISupportGetEstablishment
    {
        Task<SupportEstablishmentResponse?> Get(
       int establishmentId);
    }
}
