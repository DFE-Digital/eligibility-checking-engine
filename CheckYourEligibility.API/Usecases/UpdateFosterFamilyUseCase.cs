using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

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

        await _auditGateway.CreateAuditEntry(AuditType.FosterFamily, guid);

        return response;
    }
}