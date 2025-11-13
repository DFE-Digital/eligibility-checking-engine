using System.Text.Json;
using System.Text.Json.Nodes;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for creating or updating a user.
/// </summary>
public interface ICreateRateLimitEventUseCase
{
    /// <summary>
    ///     Execute the use case.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<bool> Execute(RateLimiterMiddlewareOptions options);

}

public class CreateRateLimitEventUseCase : ICreateRateLimitEventUseCase
{
    private readonly IRateLimit _rateLimitGateway;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CreateRateLimitEventUseCase> _logger;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="rateLimitGateway"></param>
    /// <param name="httpContextAccessor"></param>
    public CreateRateLimitEventUseCase(IRateLimit rateLimitGateway, IHttpContextAccessor httpContextAccessor, ILogger<CreateRateLimitEventUseCase> logger)
    {
        _rateLimitGateway = rateLimitGateway;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }
    
    /// <summary>
    ///     Executes logic to determine if a request is permitted for a particular rate limiter based on the RateLimiterMiddlewareOptions
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<bool> Execute(RateLimiterMiddlewareOptions options)
    {
        var localAuthorityIds = _httpContextAccessor.HttpContext?.User.GetSpecificScopeIds("local_authority");
        //Early exit if no LA id
        if (localAuthorityIds.IsNullOrEmpty() || localAuthorityIds.SequenceEqual([0]))
        {
            return true;
        }
        string partition = $"{options.PartionName}-{localAuthorityIds[0]}";

        int querySize = await getQuerySize(_httpContextAccessor.HttpContext);

        RateLimitEvent rlEvent = new RateLimitEvent
        {
            RateLimitEventId = Guid.NewGuid().ToString(),
            PartitionName = partition,
            TimeStamp = DateTime.UtcNow,
            QuerySize = querySize,
            Accepted = true
        };

        await _rateLimitGateway.Create(rlEvent);
        int currentRate = await _rateLimitGateway.GetQueriesInWindow(rlEvent.PartitionName, rlEvent.TimeStamp, options.WindowLength);
        if (querySize > options.PermitLimit - currentRate)
        {
            _httpContextAccessor.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            _httpContextAccessor.HttpContext.Response.Headers.Append("Retry-After", options.WindowLength.TotalSeconds.ToString());
            await _rateLimitGateway.UpdateStatus(rlEvent.RateLimitEventId, false);
            _logger.LogWarning($"RateLmiter partition {rlEvent.PartitionName} rejected request with size {querySize}");
            return false;
        }
        else
        {
            return true;
        }
    }

    private async Task<int> getQuerySize(HttpContext httpContext)
    {
        if (httpContext.Request.Path.ToString().Contains("bulk-check"))
        {
            httpContext.Request.EnableBuffering();
            var body = await JsonSerializer.DeserializeAsync<JsonObject>(httpContext.Request.Body);
            httpContext.Request.Body.Position = 0;
            JsonNode data;
            if (body.TryGetPropertyValue("data", out data))
            {
                return data.AsArray().Count;
            }
        }
        return 1;
    }
}