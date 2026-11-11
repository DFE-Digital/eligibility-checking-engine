using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.Core.Gateways;

public class RateLimitGateway : IRateLimit
{
    private readonly IEligibilityCheckContext _db;

    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public RateLimitGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IConfiguration configuration)
    {
        _logger = logger.CreateLogger("RateLimitService");
        _db = dbContext;
        _configuration = configuration;
    }

    /// <summary>
    ///     Creates a rate limit event
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public async Task Create(RateLimitEvent item)
    {
        _db.RateLimitEvents.Add(item);
        await _db.SaveChangesAsync();
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
        if (rateLimitEvent != null)
        {
            rateLimitEvent.Accepted = accepted;
            _db.RateLimitEvents.Update(rateLimitEvent);
            _db.SaveChanges();
        }
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

    /// <summary>
    ///     Removes all events that are beyond the given retention period
    /// </summary>
    /// <returns></returns>
    public async Task CleanUpRateLimitEvents()
    {
        try
        {
            var retentionDays = _configuration.GetValue<int>("RateLimit:RetentionDays");
            if (retentionDays > 0)
            {
                var retentionCutOff = DateTime.UtcNow.AddDays(-retentionDays);
                _logger.LogInformation($"Cleaning up rate limit events older than {retentionCutOff}");

                var rowsDeleted = await _db.RateLimitEvents
                    .Where(x => x.TimeStamp < retentionCutOff)
                    .ExecuteDeleteAsync();
                _logger.LogInformation($"Deleted {rowsDeleted} rate limit events older than {retentionCutOff}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning up rate limit events");
        }
    }
}