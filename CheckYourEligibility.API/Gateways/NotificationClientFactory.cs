using CheckYourEligibility.API.Gateways.Interfaces;
using Notify.Client;
using Notify.Interfaces;

namespace CheckYourEligibility.API.Gateways;

public class NotificationClientFactory : INotificationClientFactory
{
    public INotificationClient CreateClient(string apiKey)
    {
        return new NotificationClient(apiKey);
    }
}
