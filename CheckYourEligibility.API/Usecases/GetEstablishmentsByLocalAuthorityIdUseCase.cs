using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

public interface IGetEstablishmentsByLocalAuthorityIdUseCase
{
    Task<EstablishmentResponse> Execute(int localAuthorityId);
}

public class GetEstablishmentsByLocalAuthorityIdUseCase : IGetEstablishmentsByLocalAuthorityIdUseCase
{
    private readonly ILocalAuthority _localAuthorityGateway;
    private readonly ILogger<IGetEstablishmentsByLocalAuthorityIdUseCase> _logger;

    public GetEstablishmentsByLocalAuthorityIdUseCase(ILocalAuthority localAuthorityGateway, ILogger<GetEstablishmentsByLocalAuthorityIdUseCase> logger)
    {
        _localAuthorityGateway = localAuthorityGateway;
        _logger = logger;
    }

    public async Task<EstablishmentResponse> Execute(int localAuthorityId)
    {
        if (localAuthorityId <= 0)
        {
            _logger.LogWarning("Invalid localAuthorityId received: {LocalAuthorityId}", localAuthorityId);

            throw new NotFoundException("Invalid local authority");
        }

        var data = await _localAuthorityGateway
            .GetEstablishmentsByLocalAuthorityId(localAuthorityId);

        return new EstablishmentResponse
        {
            Data = data
        };
    }
}