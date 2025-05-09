using System.Security.Claims;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace CheckYourEligibility.API.Telemetry;

public class UserTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext == null || httpContext.User == null)
            // No HttpContext or User available
            return;

        var user = httpContext.User;

        if (user.Identity!.IsAuthenticated)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            var userName = user.Identity.Name ?? string.Empty;

            // Attach user information to telemetry context
            telemetry.Context.User.AuthenticatedUserId = userId;
            telemetry.Context.User.AccountId = userEmail;
            telemetry.Context.User.Id = userName;

            // Attach user information to custom properties if supported
            if (telemetry is ISupportProperties telemetryWithProperties)
            {
                telemetryWithProperties.Properties["UserId"] = userId;
                telemetryWithProperties.Properties["UserEmail"] = userEmail;
                telemetryWithProperties.Properties["UserName"] = userName;
            }
        }
    }
}