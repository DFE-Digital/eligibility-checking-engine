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
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;



    public StorageQueueGateway(ILoggerFactory logger,
        QueueServiceClient queueClientGateway,
        IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _configuration = configuration;

        var standardQueueName = _configuration.GetValue<string>("QueueFsmCheckStandard");
        var bulkQueueName = _configuration.GetValue<string>("QueueFsmCheckBulk");

        if (standardQueueName != "notSet")
            _queueClientStandard = queueClientGateway.GetQueueClient(standardQueueName);

        if (bulkQueueName != "notSet")
            _queueClientBulk = queueClientGateway.GetQueueClient(bulkQueueName);
    }

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task<QueueMessage[]> ProcessQueueAsync(string queName)
    {

        QueueMessage[] retrievedMessages = [];
        QueueClient queueClient = SetQueueClient(queName);

        retrievedMessages = await queueClient.ReceiveMessagesAsync(_configuration.GetValue<int>("QueueFetchSize"));
        return retrievedMessages;
    }

    public async Task DeleteMessageAsync(QueueMessage message, string queueName)
    {

        QueueClient queueClient = SetQueueClient(queueName);
        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);

    }
    public async Task UpdateMessageAsync(QueueMessage message, string queueName, int visibilityTimeout)
    {

        QueueClient queueClient = SetQueueClient(queueName);
        await queueClient.UpdateMessageAsync(
                           message.MessageId,
                           message.PopReceipt,
                           message.Body,
                           TimeSpan.FromSeconds(visibilityTimeout));


    }

    #region Private
    private QueueClient SetQueueClient(string queueName)
    {

        if (queueName == _configuration.GetValue<string>("QueueFsmCheckStandard"))
        {
            return _queueClientStandard;
        }

        else if (queueName == _configuration.GetValue<string>("QueueFsmCheckBulk"))
        {
            return _queueClientBulk;
        }

        else
        {
            throw new Exception($"invalid queue {queueName}.");
        }

    }
    #endregion
}