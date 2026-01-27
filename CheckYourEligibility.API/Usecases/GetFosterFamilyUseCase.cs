using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IGetFosterFamilyUseCase
{
    Task<FosterFamilyResponse> Execute(string guid);
}

public class GetFosterFamilyUseCase : IGetFosterFamilyUseCase
{
    private readonly IFosterFamily _fosterFamilyGateway;
    private readonly IAudit _auditGateway;

    public GetFosterFamilyUseCase(IFosterFamily fosterFamilyGateway, IAudit auditGateway)
    {
        _fosterFamilyGateway = fosterFamilyGateway;
        _auditGateway = auditGateway;
    }
    

    /// <summary>
    /// Gets an foster family by guid
    /// </summary>
    /// <param name="guid">The foster family guid</param>
    /// <returns>The foster family response</returns>
    public async Task<FosterFamilyResponse> Execute(string guid)
    {
        FosterFamilyResponse? response = await _fosterFamilyGateway.GetFosterFamily(guid);
        
        if (response == null) return null!;
        
        await _auditGateway.CreateAuditEntry(AuditType.FosterFamily, guid);

        return response;
    }
}

