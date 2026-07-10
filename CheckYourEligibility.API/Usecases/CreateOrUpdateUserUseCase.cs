using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Creates a new user or updates an existing user's last login timestamp.
/// </summary>
public interface ICreateOrUpdateUserUseCase
{
    /// <summary>
    /// Creates or updates a user using the supplied request data.
    /// </summary>
    /// <param name="model">
    /// The user creation request.
    /// </param>
    /// <returns>
    /// A response containing the user identifier.
    /// </returns>
    Task<UserSaveItemResponse> Execute(UserCreateRequest model);
}

public class CreateOrUpdateUserUseCase : ICreateOrUpdateUserUseCase
{
    private readonly IUsers _userGateway;

    public CreateOrUpdateUserUseCase(IUsers userGateway)
    {
        _userGateway = userGateway;
    }

    /// <summary>
    /// Executes the create or update user use case.
    /// </summary>
    /// <param name="model">
    /// The user creation request.
    /// </param>
    /// <returns>
    /// A response containing the user identifier.
    /// </returns>
    public async Task<UserSaveItemResponse> Execute(UserCreateRequest model)
    {
        if (model == null)
            throw new UserSaveException("User request is required.");

        var userId = await _userGateway.Create(model);

        return new UserSaveItemResponse
        {
            Data = userId
        };
    }
}