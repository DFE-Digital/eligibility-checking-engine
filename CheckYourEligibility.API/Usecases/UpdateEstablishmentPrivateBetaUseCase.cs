using CheckYourEligibility.API.Gateways.Interfaces;
namespace CheckYourEligibility.API.Usecases
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
