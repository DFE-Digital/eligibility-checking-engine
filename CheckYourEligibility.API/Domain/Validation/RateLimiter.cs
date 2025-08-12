using System.Text.Json.Nodes;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.UseCases;

public class RateLimiterMiddlewareOptions
{
    public string PartionName { get; set; }
    public TimeSpan WindowLength { get; set; }
    public int PermitLimit { get; set; }
}

public class RateLimiter
{
    private readonly RequestDelegate _next;
    private RateLimiterMiddlewareOptions _options;

    public RateLimiter(RequestDelegate next, RateLimiterMiddlewareOptions options)
    {
        _next = next;
        _options = options;
    }
    public async Task InvokeAsync(HttpContext httpContext, ICreateRateLimitEventUseCase rateLimitUseCase)
    {
        if (await rateLimitUseCase.Execute(httpContext, _options))
        {
            await _next(httpContext);
        }
        return;
        
    }
}

public static class RateLimiterExtensions
{
    public static IApplicationBuilder UseCustomRateLimiter(
        this IApplicationBuilder builder, RateLimiterMiddlewareOptions options)
    {
        return builder.UseMiddleware<RateLimiter>(options);
    }
}