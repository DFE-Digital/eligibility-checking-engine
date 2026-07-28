using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for creating or updating a user.
/// </summary>
public interface ICreateOrUpdateFSMParentUserUseCase
{
    /// <summary>
    ///     Execute the use case.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="meta"></param>
    /// <returns></returns>
    Task<UserSaveItemResponse> Execute(UserCreateRequest model);
}

public class CreateOrUpdateFSMParentUserUseCase : ICreateOrUpdateFSMParentUserUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IUsers _userGateway;

    public CreateOrUpdateFSMParentUserUseCase(IUsers userGateway, IAudit auditGateway)
    {
        _userGateway = userGateway;
        _auditGateway = auditGateway;
    }

    public async Task<UserSaveItemResponse> Execute(UserCreateRequest model)
    {
        var response = await _userGateway.CreateOrUpdateFSMParentUser(model);


        return new UserSaveItemResponse { Data = response };
    }
}