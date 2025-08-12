using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Extensions;
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
    /// <param name="httpContext"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<bool> Execute(HttpContext httpContext, RateLimiterMiddlewareOptions options);

}

public class CreateRateLimitEventUseCase : ICreateRateLimitEventUseCase
{
    private readonly IRateLimit _rateLimitGateway;

    public CreateRateLimitEventUseCase(IRateLimit rateLimitGateway)
    {
        _rateLimitGateway = rateLimitGateway;
    }
    public async Task<bool> Execute(HttpContext httpContext, RateLimiterMiddlewareOptions options)
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
        int currentRate = await _rateLimitGateway.GetQueriesInWindow(partition, rlEvent.TimeStamp, options.WindowLength);
        if (querySize > options.PermitLimit - currentRate)
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return false;
            //TODO: Determine retry period
        }
        else
        {
            // Update the status to accepted
            await _rateLimitGateway.UpdateStatus(rlEvent.RateLimitEventId, true);
            return true;
        }
    }

    private string getPartition(string partitionName, HttpContext httpContext)
    {
        var claimAuthorityIds = httpContext.User.GetLocalAuthorityIds("local_authority"); //TODO: Read this from configuration
        //TODO: Can a request have multiple authorities? Should we add to a partition for each?
        int authorityId = claimAuthorityIds[0]; //Currently just gets the first claim
        return $"{partitionName}-{authorityId}";
        //TODO: The policy name should be parameterised. Otherwise the query is stored n times, 
        // where n is the number of policies that are active e.g. 2 times if we have 2 policies for different time periods
        // Alternatively we would have to be able to chain the policies together, and only write to db once across all policies
        // If we share partition names for policies that are could be chained e.g. apply several window/limit combos per endpoint,
        // Then we would need a mechanism to make sure we only write to db at the end of the chain. Also makes it hard to determine 
        // the retry period accurately as a combination.
        // TODO: If we can't determine an id e.g. if none was provided, default to using ip address
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