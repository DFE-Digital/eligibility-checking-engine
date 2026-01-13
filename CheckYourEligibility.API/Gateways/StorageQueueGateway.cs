// Ignore Spelling: Fsm

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;

namespace CheckYourEligibility.API.Gateways;

public class StorageQueueGateway : IStorageQueue
{
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;

    private readonly ILogger _logger;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;
    private ICheckEligibility _checkEligibilityGateway;
    private ICheckingEngine _checkingEngineGateway;


    public StorageQueueGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext,
        QueueServiceClient queueClientGateway,
        IConfiguration configuration, ICheckEligibility checkEligibilityGateway, ICheckingEngine checkingEngineGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _configuration = configuration;
        _checkEligibilityGateway = checkEligibilityGateway;
        _checkingEngineGateway = checkingEngineGateway;

        setQueueStandard(_configuration.GetValue<string>("QueueFsmCheckStandard"), queueClientGateway);
        setQueueBulk(_configuration.GetValue<string>("QueueFsmCheckBulk"), queueClientGateway);
    }

    #region Private

    //TODO: These two methods are ridiculously ugly. Do it in the constructor instead
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

    //TODO: This method should return a list of IDs, that the bulk check usecase iterates over and sends to single check use case
    [ExcludeFromCodeCoverage(Justification = "Queue is external dependency.")]
    public async Task<QueueMessage[]> ProcessQueueAsync(string queName)
    {
      
        QueueMessage[] retrievedMessages = [];
        QueueClient queueClient = SetQueueClient(queName);

        retrievedMessages = await queueClient.ReceiveMessagesAsync(_configuration.GetValue<int>("QueueFetchSize"));  
          return retrievedMessages;
    }
    #endregion

    public async Task DeleteMessageAsync(QueueMessage message , string queueName) {

        QueueClient queueClient = SetQueueClient(queueName);
        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);

    }
    private QueueClient SetQueueClient(string queueName) {

        if (queueName == _configuration.GetValue<string>("QueueFsmCheckStandard"))
        {
            return _queueClientStandard;
        }

        else if (queueName == _configuration.GetValue<string>("QueueFsmCheckBulk"))
        {
            return _queueClientBulk;
        }

        else {
            throw new Exception($"invalid queue {queueName}.");
        }
        
    }
}