using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;

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

    public CreateRateLimitEventUseCase(IRateLimit rateLimitGateway, IHttpContextAccessor httpContextAccessor)
    {
        _rateLimitGateway = rateLimitGateway;
        _httpContextAccessor = httpContextAccessor;
    }
    public async Task<bool> Execute(RateLimiterMiddlewareOptions options)
    {
        var localAuthorityIds = _httpContextAccessor.HttpContext.User.GetLocalAuthorityIds("local_authority");
        //Early exit if no LA id
        if (localAuthorityIds.IsNullOrEmpty() || localAuthorityIds.SequenceEqual([0]))
        {
            //TODO: Determine if there's any value in still writing these events to the db
            //TODO: Determine if null/empty should be handled differently as it signifies an attempt without the correct scope
            return true;
        }
        string partition = $"{options.PartionName}-{localAuthorityIds[0]}"; //Currently just takes the first value, can there be multiple?
        
        int querySize = await getQuerySize(_httpContextAccessor.HttpContext);

        RateLimitEvent rlEvent = new RateLimitEvent
        {
            RateLimitEventId = Guid.NewGuid().ToString(),
            PartitionName = partition,
            TimeStamp = DateTime.UtcNow, //TODO: Maybe take from when the http request was received?
            QuerySize = querySize,
            Accepted = false //TODO: Use a different status when we're yet to determine if it's permitted
        };

        await _rateLimitGateway.Create(rlEvent);
        int currentRate = await _rateLimitGateway.GetQueriesInWindow(partition, rlEvent.TimeStamp, options.WindowLength);
        if (querySize > options.PermitLimit - currentRate)
        {
            _httpContextAccessor.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            _httpContextAccessor.HttpContext.Response.Headers.Append("Retry-After", options.WindowLength.TotalSeconds.ToString());
            return false;
            //TODO: Optional determine retry period based on a db query
        }
        else
        {
            // Update the status to accepted
            await _rateLimitGateway.UpdateStatus(rlEvent.RateLimitEventId, true);
            return true;
        }
    }

    private async Task<int> getQuerySize(HttpContext httpContext)
    {
        //TODO: Rework with better error/null handling
        if (httpContext.Request.Path.ToString().Contains("bulk-check"))
        {
            httpContext.Request.EnableBuffering();
            var body = await System.Text.Json.JsonSerializer.DeserializeAsync<JsonObject>(httpContext.Request.Body);
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