using CheckYourEligibility.Core.UseCases;
using Microsoft.AspNetCore.Http;

namespace CheckYourEligibility.Core.Domain.Middleware;

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
        if (await rateLimitUseCase.Execute(_options))
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