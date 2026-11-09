using CheckYourEligibility.Core.Gateways.Interfaces;
namespace CheckYourEligibility.Core.UseCases
{
    public interface IUpdateEstablishmentPrivateBetaUseCase
    {
        Task<bool> Execute(int establishmentId, bool inPrivateBeta);
    }
    public class UpdateEstablishmentPrivateBetaUseCase : IUpdateEstablishmentPrivateBetaUseCase
    {
        private readonly IEstablishmentManagement _gateway;

        public UpdateEstablishmentPrivateBetaUseCase(IEstablishmentManagement gateway)
        {
            _gateway = gateway;
        }

        public async Task<bool> Execute(int establishmentId, bool inPrivateBeta)
        {
            return await _gateway.UpdatePrivateBeta(establishmentId, inPrivateBeta);
        }
    }
}
