// Ignore Spelling: Fsm

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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
        _logger = logger.CreateLogger("StorageQueueGateway");
        _configuration = configuration;
        _queueServiceClient = queueServiceClient;
    }

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task<QueueMessage[]> ProcessQueueAsync(string queName)
    {
        QueueClient queueClient = GetQueueClient(queName);
        QueueMessage[] retrievedMessages = await queueClient.ReceiveMessagesAsync(_configuration.GetValue<int>($"Queue:Settings:{queName}:FetchSize"));
        return retrievedMessages;
    }

    public async Task DeleteMessageAsync(QueueMessage message, string queueName)
    {
        try
        {
            QueueClient queueClient = GetQueueClient(queueName);
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
        }
        catch (Exception ex) {

            string checkId = JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(message.Body)).Guid;
            _logger.LogError(ex, "Check:{checkId}, Action:DeleteMessageFromQueue, Status:Failed", checkId);
            throw;
        
        }
    }

    public async Task UpdateMessageAsync(QueueMessage message, string queueName, int visibilityTimeout)
    {
        try
        {
            QueueClient queueClient = GetQueueClient(queueName);
            await queueClient.UpdateMessageAsync(
                               message.MessageId,
                               message.PopReceipt,
                               message.Body,
                               TimeSpan.FromSeconds(visibilityTimeout));
          
        }

        catch (Exception ex) {
            string checkId = JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(message.Body)).Guid;
            _logger.LogError(ex, "Check:{checkId}, Action:UpdateQueueMessage, Status:Failed", checkId);
            throw;
        }
        
    }


    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task SendMessage(EligibilityCheck item, string queueName)
    {
        try
        {
            var queueClient = GetQueueClient(queueName);
            await queueClient.SendMessageAsync(
                JsonConvert.SerializeObject(new QueueMessageCheck
                {
                    Type = item.Type.ToString(),
                    Guid = item.EligibilityCheckID,
                    ProcessUrl = $"{CheckLinks.ProcessLink}{item.EligibilityCheckID}",
                    SetStatusUrl = $"{CheckLinks.GetLink}{item.EligibilityCheckID}/status"
                }));

            _logger.LogInformation(" Check:{checkId}, Action:SendMessageToQueue, Status:Success.", item.EligibilityCheckID);
        }
        catch (Exception ex) {

            _logger.LogError(ex, "Check:{checkId}, Action:SendMessageToQueue, Status:Failed", item.EligibilityCheckID);
            throw;
        }
        
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