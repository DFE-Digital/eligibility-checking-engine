using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;

public interface IGetEstablishmentsByMultiAcademyTrustIdUseCase
{
    Task<EstablishmentResponse> Execute(int multiAcademyTrustId);
}

public class GetEstablishmentsByMultiAcademyTrustIdIdUseCase : IGetEstablishmentsByMultiAcademyTrustIdUseCase
{
    private readonly IMultiAcademyTrust _multiAcademyTrustGateway;
    private readonly ILogger<GetEstablishmentsByMultiAcademyTrustIdIdUseCase> _logger;

    public GetEstablishmentsByMultiAcademyTrustIdIdUseCase(IMultiAcademyTrust multiAcademyTrustGateway, ILogger<GetEstablishmentsByMultiAcademyTrustIdIdUseCase> logger)
    {
        _multiAcademyTrustGateway = multiAcademyTrustGateway;
        _logger = logger;
    }

    public async Task<EstablishmentResponse> Execute(int multiAcademyTrustId)
    {
        if (multiAcademyTrustId <= 0)
        {
            _logger.LogWarning("Invalid multiAcademyTrustId received: {multiAcademyTrustId}", multiAcademyTrustId);

            throw new NotFoundException("Invalid Multi Academy Trust");
        }

        var data = await _multiAcademyTrustGateway
            .GetEstablishmentsByMultiAcademyTrustId(multiAcademyTrustId);

        return new EstablishmentResponse
        {
            Data = data
        };
    }
}