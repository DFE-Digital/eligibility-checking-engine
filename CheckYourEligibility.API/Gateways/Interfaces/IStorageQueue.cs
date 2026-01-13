using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IStorageQueue
{

    Task<List<string>> ProcessQueueAsync(string queue);
}