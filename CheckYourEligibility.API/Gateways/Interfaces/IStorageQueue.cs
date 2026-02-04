using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IStorageQueue
{

    Task<QueueMessage[]> ProcessQueueAsync(string queue);
    Task DeleteMessageAsync(QueueMessage message, string queueName);
    Task UpdateMessageAsync(QueueMessage message, string queueName, int visibilityTimeout);

}