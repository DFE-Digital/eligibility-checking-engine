using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CheckYourEligibility.API.Domain;
using NetTopologySuite.GeometriesGraph;
using Newtonsoft.Json;

public class RateLimiter
{
    private readonly RequestDelegate _next;
    private readonly IEligibilityCheckContext _db;
    //private readonly IHttpContextAccessor _httpContextAccessor;
    private TimeSpan _windowLength;
    private int _permitLimit;

    public RateLimiter(RequestDelegate next,
        TimeSpan windowLength,
        int permitLimit
        )
    {
        _next = next;
        _windowLength = windowLength;
        _permitLimit = permitLimit;
    }

    private int getCapacity(string partition, DateTime requestTimeStamp)
    {
        int checksInWindow = 0;  //db.RateLimitEvents
            //.Where(x => x.PartitionName == partition && x.TimeStamp >= requestTimeStamp.Subtract(_windowLength))
            //.Sum(x => x.QuerySize);
        return _permitLimit - checksInWindow;
    }

    private string getPartition(HttpContext httpContext)
    {
        var headers = httpContext.Request.Headers; //TODO: Or is this contained within the scope body?
        int authorityId = 0; //TODO Get authority id from scope in headers
        return $"authority-id-policy-{authorityId}";
        //TODO: The policy name should be parameterised. Otherwise the query is stored n times, 
        // where n is the number of policies that are active e.g. 2 times if we have 2 policies for different time periods
        // Alternatively we would have to be able to chain the policies together, and only write to db once across all policies
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

    public async Task InvokeAsync(HttpContext httpContext, IEligibilityCheckContext db)
    {
        string partition = getPartition(httpContext);
        int querySize = await getQuerySize(httpContext);
        //Firstly record the event
        RateLimitEvent rlEvent = new RateLimitEvent
        {
            RateLimitEventId = Guid.NewGuid().ToString(),
            PartitionName = partition,
            TimeStamp = DateTime.UtcNow, //TODO: Maybe take from when the http request was received?
            QuerySize = querySize,
            Accepted = false //TODO: Use a different status when we're yet to determine if it's permitted
        };
        //var checksInWindow = db.RateLimitEvents.Where(x => x.PartitionName == partition).Sum(x => x.QuerySize);
        //await db.RateLimitEvents.AddAsync(rlEvent);
        //await db.SaveChangesAsync();
        await _next(httpContext);

        /*
                //var checksInWindow = db.RateLimitEvents
                //    .Where(x => x.PartitionName == partition && x.TimeStamp >= rlEvent.TimeStamp.Subtract(_windowLength));
                //.Sum(x => x.QuerySize);

                //Then determine whether the event was viable
                if (querySize > getCapacity(partition, rlEvent.TimeStamp))
                //if (querySize > _permitLimit - checksInWindow)
                {
                    rlEvent.Accepted = false;
                    //await _db.SaveChangesAsync();
                    //TODO: Maybe log the rejection
                    //TODO: Set a 429 response and queryRetryTime
                    httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    return;
                }
                else
                {
                    rlEvent.Accepted = true;
                    //await _db.SaveChangesAsync();
                    await _next(httpContext);
                }
                */
    }
}

public static class RateLimiterExtensions
{
    public static IApplicationBuilder UseCustomRateLimiter(
        this IApplicationBuilder builder, TimeSpan windowSize, int permitLimit)
    {
        return builder.UseMiddleware<RateLimiter>(windowSize, permitLimit);
    }
}