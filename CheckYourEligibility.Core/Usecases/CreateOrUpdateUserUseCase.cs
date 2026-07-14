using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

/// <summary>
///     Interface for creating or updating a user.
/// </summary>
public interface ICreateOrUpdateUserUseCase
{
    /// <summary>
    ///     Execute the use case.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="meta"></param>
    /// <returns></returns>
    Task<UserSaveItemResponse> Execute(UserCreateRequest model);
}

public class CreateOrUpdateUserUseCase : ICreateOrUpdateUserUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IUsers _userGateway;

    public CreateOrUpdateUserUseCase(IUsers userGateway, IAudit auditGateway)
    {
        _userGateway = userGateway;
        _auditGateway = auditGateway;
    }

    public async Task<UserSaveItemResponse> Execute(UserCreateRequest model)
    {
        var response = await _userGateway.Create(model.Data);


        return new UserSaveItemResponse { Data = response };
    }
}