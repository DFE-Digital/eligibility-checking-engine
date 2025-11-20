using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IStorageQueueMessage
{
    Task<string> SendMessage(EligibilityCheck item);
}