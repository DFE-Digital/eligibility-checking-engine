using Azure.Storage.Queues.Models;
using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IStorageQueue
{

    Task<QueueMessage[]> ProcessQueueAsync(string queue);
    Task DeleteMessageAsync(QueueMessage message, string queueName);
    Task UpdateMessageAsync(QueueMessage message, string queueName, int visibilityTimeout);
    Task SendMessage(EligibilityCheck item, string queueName);

}