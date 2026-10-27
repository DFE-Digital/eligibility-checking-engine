using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.Usecases;

public interface ISupportSearchEstablishmentUseCase
{
    Task<SupportEstablishmentSearchResponse> Execute(
        SupportEstablishmentSearchRequest request);
}

public class SupportSearchEstablishmentUseCase
    : ISupportSearchEstablishmentUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IEstablishmentManagement _gateway;

    public SupportSearchEstablishmentUseCase(
        IEstablishmentManagement gateway,
        IAudit auditGateway)
    {
        _gateway = gateway;
        _auditGateway = auditGateway;
    }

    public async Task<SupportEstablishmentSearchResponse> Execute(
        SupportEstablishmentSearchRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.PageNumber <= 0)
        {
            request.PageNumber = 1;
        }

        if (request.PageSize <= 0)
        {
            request.PageSize = 20;
        }

       

        var results = await _gateway.Search(request);

        return results;
    }
}