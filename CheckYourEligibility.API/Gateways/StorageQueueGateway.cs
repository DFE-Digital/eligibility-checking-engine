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

public class StorageQueueGateway : BaseGateway, IStorageQueue
{
    private const int SurnameCheckCharachters = 3;
    protected readonly IAudit _audit;
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;

    private readonly IEcsAdapter _ecsAdapter;
    private readonly IDwpAdapter _dwpAdapter;
    private readonly IHash _hashGateway;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;
    private string _groupId;
    private QueueClient _queueClientBulk;
    private QueueClient _queueClientStandard;
    private ICheckEligibility _checkEligibilityGateway;
    private ICheckingEngine _checkingEngineGateway;


    public StorageQueueGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        QueueServiceClient queueClientGateway,
        IConfiguration configuration, IEcsAdapter ecsAdapter, IDwpAdapter dwpAdapter, IAudit audit, IHash hashGateway, ICheckEligibility checkEligibilityGateway, ICheckingEngine checkingEngineGateway)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _mapper = mapper;
        _ecsAdapter = ecsAdapter;
        _dwpAdapter= dwpAdapter;
        _audit = audit;
        _hashGateway = hashGateway;
        _configuration = configuration;
        _checkEligibilityGateway = checkEligibilityGateway;
        _checkingEngineGateway = checkingEngineGateway;

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

    public async Task ProcessQueue(string queName)
    {
        QueueClient queue;
        if (queName == _configuration.GetValue<string>("QueueFsmCheckStandard"))
            queue = _queueClientStandard;
        else if (queName == _configuration.GetValue<string>("QueueFsmCheckBulk"))
            queue = _queueClientBulk;
        else
            throw new Exception($"invalid queue {queName}.");
        
        var sw = Stopwatch.StartNew();
        if (await queue.ExistsAsync())
        {
            QueueProperties properties = await queue.GetPropertiesAsync();

            if (properties.ApproximateMessagesCount > 0)
            {
                QueueMessage[] retrievedMessage = await queue.ReceiveMessagesAsync(_configuration.GetValue<int>("QueueFetchSize"));
                
                _logger.LogInformation($"Reading queue item in {sw.ElapsedMilliseconds} ms");
                foreach (var item in retrievedMessage)
                {
                    sw.Restart();
                    var checkData =
                        JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(item.Body));
                    try
                    {
                        var postCheckAudit = await _db.Audits.FirstOrDefaultAsync(a => a.TypeID == checkData.Guid && a.Type == AuditType.Check && a.Method == "POST");
                        string scope = string.Empty;
                        if (postCheckAudit != null && postCheckAudit.Scope != null) scope = postCheckAudit.Scope;

                        var result = await _checkingEngineGateway.ProcessCheck(checkData.Guid, new AuditData
                        {
                            Type = AuditType.Check,
                            typeId = checkData.Guid,
                            authentication = queName,
                            method = "processQue",
                            source = "queueProcess",
                            url = ".",
                            scope = scope
                        });
                        // When status is Queued For Processing, i.e. not error
                        if (result == CheckEligibilityStatus.queuedForProcessing)
                        {
                            //If item doesn't exist, or we've tried more than retry limit
                            if (result == null || item.DequeueCount >= _configuration.GetValue<int>("QueueRetries"))
                            {
                                //Delete message and update status to error
                                await _checkEligibilityGateway.UpdateEligibilityCheckStatus(checkData.Guid,
                                    new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error });
                                await queue.DeleteMessageAsync(item.MessageId, item.PopReceipt);
                            }
                        }
                        // If status is not queued for Processing, we have a conclusive answer
                        else
                        {
                            await queue.DeleteMessageAsync(item.MessageId, item.PopReceipt);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Queue processing");
                        // If we've had exceptions on this item more than retry limit
                        if (item.DequeueCount >= _configuration.GetValue<int>("QueueRetries"))
                        {
                            await _checkEligibilityGateway.UpdateEligibilityCheckStatus(checkData.Guid,
                                new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error });
                            await queue.DeleteMessageAsync(item.MessageId, item.PopReceipt);
                        }
                        else
                            await queue.UpdateMessageAsync(
                                item.MessageId,
                                item.PopReceipt,
                                item.Body,
                                TimeSpan.Zero
                            );                    
                    }
                    
                    _logger.LogInformation($"Processing queue item in {sw.ElapsedMilliseconds} ms");
                }

                properties = await queue.GetPropertiesAsync();
            }
        }
    }

    #endregion
}