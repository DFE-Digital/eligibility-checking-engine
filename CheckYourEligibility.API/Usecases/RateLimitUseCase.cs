using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for creating or updating a user.
/// </summary>
public interface ICreateRateLimitEvent
{
    /// <summary>
    ///     Execute the use case.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    Task<> Execute(RateLimitEvent item);
}

public class CreateRateLimitEventUseCase : ICreateRateLimitEvent
{
    private readonly IRateLimit _rateLimitGateway;

    public CreateRateLimitEventUseCase(IRateLimit rateLimitGateway)
    {
        _rateLimitGateway = rateLimitGateway;
    }

    public async Task<> Execute(RateLimitEvent item)
    {
        await _rateLimitGateway.Create(item);
        return;
    }
}