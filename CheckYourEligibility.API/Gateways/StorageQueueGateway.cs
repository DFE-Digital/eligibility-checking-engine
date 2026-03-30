// Ignore Spelling: Fsm

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Gateways.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Gateways;

public class StorageQueueGateway : IStorageQueue
{
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;

    private readonly ILogger _logger;
    private string _groupId;
    private Dictionary<string, QueueClient> _queues = new();
    private QueueServiceClient _queueServiceClient;


    public StorageQueueGateway(ILoggerFactory logger,
        QueueServiceClient queueServiceClient,
        IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _configuration = configuration;
        _queueServiceClient = queueServiceClient;
    }

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task<QueueMessage[]> ProcessQueueAsync(string queName)
    {

        QueueMessage[] retrievedMessages = [];
        
        QueueClient queueClient = GetQueueClient(queName);
        retrievedMessages = await queueClient.ReceiveMessagesAsync(_configuration.GetValue<int>($"Queue:Settings:{queName}:FetchSize"));
        return retrievedMessages;
    }

    public async Task DeleteMessageAsync(QueueMessage message, string queueName)
    {

        QueueClient queueClient = GetQueueClient(queueName);
        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);

    }
    public async Task UpdateMessageAsync(QueueMessage message, string queueName, int visibilityTimeout)
    {

        QueueClient queueClient = GetQueueClient(queueName);
        await queueClient.UpdateMessageAsync(
                           message.MessageId,
                           message.PopReceipt,
                           message.Body,
                           TimeSpan.FromSeconds(visibilityTimeout));


    }

    private QueueClient GetQueueClient(string queueName)
    {
        if (!_queues.ContainsKey(queueName))
        {
            _queues[queueName] = _queueServiceClient.GetQueueClient(queueName);
        }

        return _queues[queueName];
    }
}