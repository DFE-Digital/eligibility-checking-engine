using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways;

public class RateLimitGateway : BaseGateway, IRateLimit
{
    private readonly IEligibilityCheckContext _db;

    private readonly ILogger _logger;

    public RateLimitGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext)
    {
        _logger = logger.CreateLogger("RateLimitService");
        _db = dbContext;
    }

    /// <summary>
    ///     Creates a rate limit event
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public async Task Create(RateLimitEvent item)
    {
        //TODO: Should this be async??
        //TODO: What if event already exists?
        await _db.RateLimitEvents.AddAsync(item);
        await _db.SaveChangesAsync();
        return;
    }
    
    /// <summary>
    ///     Sets the status of the rateLimitEvent, reflecting the decision made of whether to permit the request
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="accepted"></param>
    /// <returns></returns>
    public async Task UpdateStatus(string guid, bool accepted)
    {
        var rateLimitEvent = _db.RateLimitEvents.Find(guid);
        //TODO: Null handling
        rateLimitEvent.Accepted = accepted;
        _db.RateLimitEvents.Update(rateLimitEvent);
        _db.SaveChanges();
        return;
    }

    /// <summary>
    ///     Gets the sum of the number of checks that have been submitted 
    /// </summary>
    /// <param name="partition"></param>
    /// <param name="eventTimeStamp"></param>
    /// <param name="windowLength"></param>
    /// <returns></returns>
    public async Task<int> GetQueriesInWindow(string partition, DateTime eventTimeStamp, TimeSpan windowLength)
    {
        return _db.RateLimitEvents.Where(x => x.PartitionName == partition &&
            x.TimeStamp < eventTimeStamp && 
            x.TimeStamp >= eventTimeStamp.Subtract(windowLength))
            .Sum(x => x.QuerySize);
    }
}