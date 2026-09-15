using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.Gateways;

public class NotifyGateway : INotify
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<NotifyGateway> _logger;
    private readonly INotificationClientFactory _notificationClientFactory;

    public NotifyGateway(INotificationClientFactory notificationClientFactory, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor, ILogger<NotifyGateway> logger)
    {
        _notificationClientFactory = notificationClientFactory;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void SendNotification(NotificationRequest notificationRequest)
    {
        var templateId =
            _configuration.GetValue<string>($"Notify:Templates:{notificationRequest.Data.Type.ToString()}");
        Deliver(templateId, notificationRequest.Data.Email, notificationRequest.Data.Personalisation);
    }

    private void Deliver(string templateId, string email, Dictionary<string, object> personalisation)
    {
        var apiKey = GetApiKey();
        if (apiKey.IsNullOrEmpty()) return;

        _notificationClientFactory.CreateClient(apiKey).SendEmail(email, templateId, personalisation);
    }

    // Clients/users listed under Notify:TestModeClients use Notify:TestKey - a GOV.UK Notify "test" API key
    // that records the notification without emailing anyone. This stops automated test runs against dev/test
    // from sending real emails while still exercising the code path. The authenticated identity's client id
    // (e.g. "Cypress") and, for shared clients whose id embeds a per-purpose username (e.g. "Omni:ece.service
    // +cypress@service.education.gov.uk"), the username portion are both checked against the configured list.
    // TestModeClients is a single comma-separated value (rather than a config array) so it can be stored as
    // one flat Azure Key Vault secret, e.g. "Cypress,ece.service+cypress@service.education.gov.uk".
    private string? GetApiKey()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var (source, userName) = user.GetCheckSourceAndUserNameFromClientId();
            var testModeClients = (_configuration.GetValue<string>("Notify:TestModeClients") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var isTestModeClient = testModeClients.Any(c =>
                string.Equals(c, source, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, userName, StringComparison.OrdinalIgnoreCase));
            if (isTestModeClient)
            {
                var testKey = _configuration.GetValue<string>("Notify:TestKey");
                if (!testKey.IsNullOrEmpty()) return testKey;

                // Fail closed: never fall back to the live key for a test-mode client, that would defeat
                // the point of Notify:TestModeClients and could send real emails from automated test runs.
                _logger.LogWarning(
                    "Notify:TestModeClients includes {ClientIdentifier} but Notify:TestKey is not configured; skipping notification instead of sending a real email.",
                    userName ?? source);
                return null;
            }
        }

        return _configuration.GetValue<string>("Notify:Key");
    }
}