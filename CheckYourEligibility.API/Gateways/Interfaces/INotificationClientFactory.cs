using Notify.Interfaces;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface INotificationClientFactory
{
    INotificationClient CreateClient(string apiKey);
}
