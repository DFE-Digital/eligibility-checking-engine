using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for processing messages from a specified queue
/// </summary>
public interface IProcessEligibilityBulkCheckUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="queue">Queue identifier</param>
    /// <returns>A message response indicating success</returns>
    Task<MessageResponse> Execute(string queue);
}

public class ProcessEligibilityBulkCheckUseCase : IProcessEligibilityBulkCheckUseCase
{
    private readonly IConfiguration _configuration;
    private readonly IStorageQueue _storageQueueGateway;
    private readonly IProcessEligibilityCheckUseCase _processEligibilityCheckUseCase;
    private ICheckEligibility _checkEligibilityGateway;
    private IDbContextFactory<EligibilityCheckContext> _dbContextFactory;
    private readonly ILogger<ProcessEligibilityBulkCheckUseCase> _logger;

    public ProcessEligibilityBulkCheckUseCase(IStorageQueue storageQueueGateway, ILogger<ProcessEligibilityBulkCheckUseCase> logger,
        IProcessEligibilityCheckUseCase processEligibilityCheckUseCase, IConfiguration configuration, ICheckEligibility checkEligibilityGateway,
        IDbContextFactory<EligibilityCheckContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _storageQueueGateway = storageQueueGateway;
        _logger = logger;
        _processEligibilityCheckUseCase = processEligibilityCheckUseCase;
        _checkEligibilityGateway = checkEligibilityGateway;
    }

    public async Task<MessageResponse> Execute(string queueName)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            _logger.LogWarning("Empty queue name provided to ProcessQueueMessagesUseCase");
            return new MessageResponse { Data = "Invalid Request." };
        }

        var retrievedItemsFromQueue = await _storageQueueGateway.ProcessQueueAsync(queueName);
        var sw = Stopwatch.StartNew();
        var st = Stopwatch.StartNew();
        int i = 1;
        if (retrievedItemsFromQueue.Count() > 0)
        {

            var tasks = retrievedItemsFromQueue.Select(async item =>

            {
                using (var dbContext = _dbContextFactory.CreateDbContext())
                {
                    sw.Restart();
                    var checkData = JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(item.Body));
                    try
                    {

                        var response = await _processEligibilityCheckUseCase.Execute(checkData.Guid, dbContext);

                     
                       _logger.LogDebug(
                        $"TimesStamp: {DateTime.UtcNow}\n"+
                        $"Item_No....{i} \n" +
                        $"Process_Time....{sw.ElapsedMilliseconds:N0} ms \n" +
                        $"Time_Elapsed....{st.ElapsedMilliseconds:N0} ms");
                        i++;
                        _logger.LogInformation($"Reading queue item in {sw.ElapsedMilliseconds} ms");

                        if ((CheckEligibilityStatus)Enum.Parse(typeof(CheckEligibilityStatus), response.Data.Status) == CheckEligibilityStatus.queuedForProcessing)
                        {
                            //If we've tried more than retry limit
                            if (item.DequeueCount >= _configuration.GetValue<int>("QueueRetries"))
                            {
                                //Delete message and update status to error
                                await _checkEligibilityGateway.UpdateEligibilityCheckStatus(checkData.Guid,
                                    new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error }, dbContext);

                                await _storageQueueGateway.DeleteMessageAsync(item, queueName);
                            }
                        }
                        else
                        {
                            try
                            {

                                await _storageQueueGateway.DeleteMessageAsync(item, queueName);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error deleting queue item");
                            }


                        }


                    }

                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Queue processing");
                        // If we've had exceptions on this item more than retry limit
                        if (item.DequeueCount >= _configuration.GetValue<int>("QueueRetries"))
                        {
                            await _checkEligibilityGateway.UpdateEligibilityCheckStatus(checkData.Guid,
                                new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error }, dbContext);
                            await _storageQueueGateway.DeleteMessageAsync(item, queueName);
                        }
                        else
                        {
                            // update message invisibility by 5 seocnds
                            await _storageQueueGateway.UpdateMessageAsync(item, queueName, 5);
                        }
                    }

                    _logger.LogInformation($"Processing queue item in {sw.ElapsedMilliseconds} ms");
                }

            }

            );

            await Task.WhenAll(tasks);

            st.Stop();

            _logger.LogInformation(
                $"Queue {queueName.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} processed successfully");


        }
        return new MessageResponse { Data = "Queue Processed." };

    }
}