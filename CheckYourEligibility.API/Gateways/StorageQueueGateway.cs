// Ignore Spelling: Fsm

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
    public async Task<List<string>> ProcessQueueAsync(string queName)
    {
        QueueClient queue;
        QueueMessage[] retrievedMessages ;
        List<string> queuedItemGuidList = [];
        var sw = Stopwatch.StartNew();
        if (queName == _configuration.GetValue<string>("QueueFsmCheckStandard"))
            queue = _queueClientStandard;
        else if (queName == _configuration.GetValue<string>("QueueFsmCheckBulk"))
            queue = _queueClientBulk;
        else
            throw new Exception($"invalid queue {queName}.");

        QueueProperties properties = await queue.GetPropertiesAsync();

        if (properties.ApproximateMessagesCount > 0) {

           retrievedMessages = await queue.ReceiveMessagesAsync(_configuration.GetValue<int>("QueueFetchSize"));
            _logger.LogInformation($"Reading queue item in {sw.ElapsedMilliseconds} ms");

            var tasks = retrievedMessages.Select(async item =>

            {
                sw.Restart();
                var checkData = JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(item.Body));
                queuedItemGuidList.Add(checkData.Guid);
            });
            await Task.WhenAll(tasks);
        }
       
        return queuedItemGuidList;
    }
    #endregion
}