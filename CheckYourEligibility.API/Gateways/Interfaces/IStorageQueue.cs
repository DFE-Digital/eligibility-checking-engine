using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IStorageQueue
{

    Task ProcessQueue(string queue);
    Task<string> SendMessage(EligibilityCheck item);
}