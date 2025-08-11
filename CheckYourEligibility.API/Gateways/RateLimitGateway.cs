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
        await _db.RateLimitEvents.AddAsync(item);
        await _db.SaveChangesAsync();
        return;
    }
}