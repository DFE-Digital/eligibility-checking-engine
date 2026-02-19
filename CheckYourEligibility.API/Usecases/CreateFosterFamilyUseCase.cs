using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateFosterFamilyUseCase
{
    /// <summary>
    /// Creates a new foster family 
    /// </summary>
    /// <param name="model">The foster family request data</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>The created foster family response</returns>
    Task<FosterFamilySaveItemResponse> Execute(FosterFamilyRequest model, List<int> allowedLocalAuthorityIds);
}

/// <summary>
/// Implementation of the create foster family use case
/// </summary>
public class CreateFosterFamilyUseCase : ICreateFosterFamilyUseCase
{
    private readonly IFosterFamily _fosterFamilyGateway;
    private readonly IAudit _auditGateway;

    /// <summary>
    /// Constructor for CreateFosterFamilyUseCase
    /// </summary>
    /// <param name="fosterFamilyGateway">The foster family gateway</param>
    /// <param name="auditGateway">The audit gateway</param>
    public CreateFosterFamilyUseCase(IFosterFamily fosterFamilyGateway, IAudit auditGateway)
    {
        _fosterFamilyGateway = fosterFamilyGateway;
        _auditGateway = auditGateway;
    }

    public async Task<FosterFamilySaveItemResponse> Execute(FosterFamilyRequest model, List<int> allowedLocalAuthorityIds)
    {
        if (model == null || model.Data == null) throw new ValidationException("Invalid request, data is required");

        var validator  = new FosterFamilyRequestValidator();
        var validationResults = validator.Validate(model);

        if (!validationResults.IsValid) throw new ValidationException(validationResults.ToString());
        
        var response = await _fosterFamilyGateway.PostFosterFamily(model.Data);

        if (response != null) await _auditGateway.CreateAuditEntry(AuditType.FosterFamily, response.FosterCarerId.ToString());

        if (response == null)
        {
            throw new Exception("Failed to create foster family");
        }

        return new FosterFamilySaveItemResponse
        {
            Data = response
        };


    }
}