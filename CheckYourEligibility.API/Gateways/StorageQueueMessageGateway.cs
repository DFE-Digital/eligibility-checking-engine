// Ignore Spelling: Fsm

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AutoMapper;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Gateways;

public class StorageQueueMessageGateway : IStorageQueueMessage
{
    private readonly IConfiguration _configuration;

    private readonly ILogger _logger;
    private string _groupId;
    private QueueServiceClient _queueServiceClient;
    private Dictionary<string,QueueClient> _queues = new();


    public StorageQueueMessageGateway(ILoggerFactory logger,
        QueueServiceClient queueServiceClient,
        IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _configuration = configuration;
        _queueServiceClient = queueServiceClient;
    }

    #region Private

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task<string> SendMessage(EligibilityCheck item, QueueClient queueClient)
    {
        var queueName = _configuration[$"Queue:{(item.BulkCheckID.IsNullOrEmpty()?"Single":"Bulk")}:{item.Type.ToString()}"];
        
        await queueClient.SendMessageAsync(
            JsonConvert.SerializeObject(new QueueMessageCheck
            {
                Type = item.Type.ToString(),
                Guid = item.EligibilityCheckID,
                ProcessUrl = $"{CheckLinks.ProcessLink}{item.EligibilityCheckID}",
                SetStatusUrl = $"{CheckLinks.GetLink}{item.EligibilityCheckID}/status"
            }));

        

        return queueClient.Name;
    }

    public QueueClient GetQueueClient(string queueName)
    {
        if (!_queues.ContainsKey(queueName))
        {
            _queues[queueName] = _queueServiceClient.GetQueueClient(queueName);
        }

        return _queues[queueName];
    }

    #endregion
}