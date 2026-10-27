using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
namespace CheckYourEligibility.API.Usecases
{
    public interface ISupportGetEstablishmentUseCase
    {
        Task<SupportEstablishmentResponse?> Execute(int establishmentId);
    }
    public class SupportGetEstablishmentUseCase : ISupportGetEstablishmentUseCase
    {
        private readonly ISupportGetEstablishment _gateway;

        public SupportGetEstablishmentUseCase(
            ISupportGetEstablishment gateway)
        {
            _gateway = gateway;
        }

        public async Task<SupportEstablishmentResponse?> Execute(
            int establishmentId)
        {
            if (establishmentId <= 0)
            {
                throw new ArgumentException(
                    nameof(establishmentId));
            }

            return await _gateway.Get(establishmentId);
        }
    }
}
