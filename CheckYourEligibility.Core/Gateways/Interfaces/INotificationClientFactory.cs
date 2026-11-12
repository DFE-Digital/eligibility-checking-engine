using Notify.Interfaces;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface INotificationClientFactory
{
    INotificationClient CreateClient(string apiKey);
}
