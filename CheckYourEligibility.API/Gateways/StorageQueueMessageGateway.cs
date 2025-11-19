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

public class StorageQueueMessageGateway : BaseGateway, IStorageQueueMessage
{
    private readonly IConfiguration _configuration;

    private readonly ILogger _logger;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;


    public StorageQueueMessageGateway(ILoggerFactory logger,
        QueueServiceClient queueClientGateway,
        IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _configuration = configuration;

        setQueueStandard(_configuration.GetValue<string>("QueueFsmCheckStandard"), queueClientGateway);
        setQueueBulk(_configuration.GetValue<string>("QueueFsmCheckBulk"), queueClientGateway);
    }

    #region Private

    [ExcludeFromCodeCoverage]
    private void setQueueStandard(string queName, QueueServiceClient queueClientGateway)
    {
        if (queName != "notSet") _queueClientStandard = queueClientGateway.GetQueueClient(queName);
    }

    [ExcludeFromCodeCoverage]
    private void setQueueBulk(string queName, QueueServiceClient queueClientGateway)
    {
        if (queName != "notSet") _queueClientBulk = queueClientGateway.GetQueueClient(queName);
    }

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task<string> SendMessage(EligibilityCheck item)
    {
        var queueName = string.Empty;
        if (_queueClientStandard != null)
        {
            if (item.BulkCheckID.IsNullOrEmpty())
            {
                await _queueClientStandard.SendMessageAsync(
                    JsonConvert.SerializeObject(new QueueMessageCheck
                    {
                        Type = item.Type.ToString(),
                        Guid = item.EligibilityCheckID,
                        ProcessUrl = $"{CheckLinks.ProcessLink}{item.EligibilityCheckID}",
                        SetStatusUrl = $"{CheckLinks.GetLink}{item.EligibilityCheckID}/status"
                    }));

                LogQueueCount(_queueClientStandard);
                queueName = _queueClientStandard.Name;
            }
            else
            {
                await _queueClientBulk.SendMessageAsync(
                    JsonConvert.SerializeObject(new QueueMessageCheck
                    {
                        Type = item.Type.ToString(),
                        Guid = item.EligibilityCheckID,
                        ProcessUrl = $"{CheckLinks.ProcessLink}{item.EligibilityCheckID}",
                        SetStatusUrl = $"{CheckLinks.GetLink}{item.EligibilityCheckID}/status"
                    }));
                LogQueueCount(_queueClientBulk);
                queueName = _queueClientBulk.Name;
            }
        }

        return queueName;
    }

    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    private void LogQueueCount(QueueClient queue)
    {
        QueueProperties properties = queue.GetProperties();

        // Retrieve the cached approximate message count
        var cachedMessagesCount = properties.ApproximateMessagesCount;
        TrackMetric($"QueueCount:-{_queueClientStandard.Name}", cachedMessagesCount);
    }

    #endregion
}