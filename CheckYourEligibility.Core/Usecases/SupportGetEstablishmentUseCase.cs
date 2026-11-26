using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;
namespace CheckYourEligibility.Core.UseCases
{
    public interface ISupportGetEstablishmentUseCase
    {
        Task<SupportEstablishmentResponse?> Execute(int establishmentId);
    }
    public class SupportGetEstablishmentUseCase : ISupportGetEstablishmentUseCase
    {

        private readonly IEstablishmentManagement _gateway;
        public SupportGetEstablishmentUseCase(IEstablishmentManagement gateway)
        {
            _gateway = gateway;
        }

        public async Task<SupportEstablishmentResponse?> Execute(int establishmentId)
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
