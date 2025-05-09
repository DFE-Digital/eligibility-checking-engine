using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for creating or updating a user.
/// </summary>
public interface ICreateOrUpdateUserUseCase
{
    /// <summary>
    ///     Execute the use case.
    /// </summary>
    /// <param name="model"></param>
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
        if (model.Data == null)
            throw new ArgumentNullException(nameof(model));

        var response = await _userGateway.Create(model.Data);

        await _auditGateway.CreateAuditEntry(AuditType.User, response);

        return new UserSaveItemResponse { Data = response };
    }
}