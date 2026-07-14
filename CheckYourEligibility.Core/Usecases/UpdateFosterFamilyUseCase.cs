using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;

public interface IUpdateFosterFamilyUseCase
{
    Task<FosterFamilyResponse> Execute(string guid, FosterFamilyUpdateRequest model);
}

public class UpdateFosterFamilyUseCase : IUpdateFosterFamilyUseCase
{
    private readonly IFosterFamily _fosterFamilyGateway;
    private readonly IAudit _auditGateway;

    public UpdateFosterFamilyUseCase(IFosterFamily fosterFamilyGateway, IAudit auditGateway)
    {
        _fosterFamilyGateway = fosterFamilyGateway;
        _auditGateway = auditGateway;
    }

    public async Task<FosterFamilyResponse> Execute(string guid, FosterFamilyUpdateRequest model)
    {
        var response = await _fosterFamilyGateway.UpdateFosterFamily(guid, model);
        if (response == null) return null;

        return response;
    }
}