
namespace CheckYourEligibility.Core.Domain.Middleware;

public class RateLimiterMiddlewareOptions
{
    public string PartionName { get; set; }
    public TimeSpan WindowLength { get; set; }
    public int PermitLimit { get; set; }
}
