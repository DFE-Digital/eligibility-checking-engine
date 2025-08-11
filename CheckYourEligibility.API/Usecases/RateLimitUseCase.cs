using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
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
    /// <param name="item"></param>
    /// <returns></returns>
    Task Execute(HttpContext httpContext, RateLimiterMiddlewareOptions options);
    //Task Execute(RateLimitEvent item);

}

public class CreateRateLimitEventUseCase : ICreateRateLimitEventUseCase
{
    private readonly IRateLimit _rateLimitGateway;

    public CreateRateLimitEventUseCase(IRateLimit rateLimitGateway)
    {
        _rateLimitGateway = rateLimitGateway;
    }

    /*
        public async Task Execute(RateLimitEvent item)
        {
            await _rateLimitGateway.Create(item);
            return;
        }
        */

    //TODO: Create a RateLimiterOptions class and pass through to this method
    public async Task Execute(HttpContext httpContext, RateLimiterMiddlewareOptions options)
    {
        string partition = getPartition(options.PartionName, httpContext);
        int querySize = await getQuerySize(httpContext);

        RateLimitEvent rlEvent = new RateLimitEvent
        {
            RateLimitEventId = Guid.NewGuid().ToString(),
            PartitionName = partition,
            TimeStamp = DateTime.UtcNow, //TODO: Maybe take from when the http request was received?
            QuerySize = querySize,
            Accepted = false //TODO: Use a different status when we're yet to determine if it's permitted
        };

        await _rateLimitGateway.Create(rlEvent);
        //TODO create an options class and pass into this query
        int currentRate = await _rateLimitGateway.GetQueriesInWindow(partition, rlEvent.TimeStamp, options.WindowLength);
        if (querySize > options.PermitLimit - currentRate)
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
            //TODO: Determine retry period
        }
        else
        {
            //TODO: Set the status to accepted
        }
        return;
    }

    private string getPartition(string partitionName, HttpContext httpContext)
    {
        var headers = httpContext.Request.Headers; //TODO: Or is this contained within the scope body?
        int authorityId = 0; //TODO Get authority id from scope in headers
        return $"{partitionName}-{authorityId}";
        // If we share partition names for policies that are could be chained e.g. apply several window/limit combos per endpoint,
        // Then we would need a mechanism to make sure we only write to db at the end of the chain. Also makes it hard to determine 
        // the retry period accurately as a combination.
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