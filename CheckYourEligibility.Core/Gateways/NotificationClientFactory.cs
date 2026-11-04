using CheckYourEligibility.Core.Gateways.Interfaces;
using Notify.Client;
using Notify.Interfaces;

namespace CheckYourEligibility.Core.Gateways;

public class NotificationClientFactory : INotificationClientFactory
{
    public INotificationClient CreateClient(string apiKey)
    {
        return new NotificationClient(apiKey);
    }
}
