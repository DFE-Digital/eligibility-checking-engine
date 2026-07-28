using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
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
        var st = Stopwatch.StartNew();
        int i = 1;
        if (retrievedItemsFromQueue.Count() > 0)
        {

            var tasks = retrievedItemsFromQueue.Select(async item =>

            {
                using (var dbContext = _dbContextFactory.CreateDbContext())
                {
                    var sw = Stopwatch.StartNew();
                    var checkData = JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(item.Body));
                    bool deleteQueueMessage = false;
                    try
                    {
                        await Task.Delay(3);

                        _logger.LogInformation($"Item {i} - {checkData.Guid} - starting use case");

                        var response = await _processEligibilityCheckUseCase.Execute(checkData.Guid, dbContext);

                        _logger.LogInformation($"Item {i} - {checkData.Guid} - use case completed in {sw.ElapsedMilliseconds}ms\r\nResponse: {JsonConvert.SerializeObject(response)}");

                        i++;

                        if ((CheckEligibilityStatus)Enum.Parse(typeof(CheckEligibilityStatus), response.Data.Status) == CheckEligibilityStatus.queuedForProcessing)
                        {
                            //If we've tried more than retry limit
                            if (item.DequeueCount >= _configuration.GetValue<int>($"Queue:Settings:{queueName}:Retries"))
                            {
                                _logger.LogInformation($"Item {i} - {checkData.Guid} - exceeded retry limit. Marking as error and for deletion from queue");

                                // Update check status to error
                                await _checkEligibilityGateway.UpdateEligibilityCheckStatus(checkData.Guid,
                                    new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error }, dbContext);

                                // Mark for deletion from queue
                                deleteQueueMessage = true;
                            }
                            else
                            {
                                _logger.LogInformation($"Item {i} - {checkData.Guid} - re-queued for processing");
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Item {i} - {checkData.Guid} - processed successfully. Marking for deletion from queue");

                            // Mark for deletion from queue
                            deleteQueueMessage = true;
                        }
                    }
                    catch (NotFoundException ex)
                    {
                        //if check record is not found in the database due to unexpected roll-back delete the message from the queue.
                        _logger.LogInformation($"Item {i} - {checkData.Guid} - GUID not found in database. Deleting from queue.");
                        deleteQueueMessage = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Queue processing");
                        // If we've had exceptions on this item more than retry limit
                        if (item.DequeueCount >= _configuration.GetValue<int>($"Queue:Settings:{queueName}:Retries"))
                        {
                            await _checkEligibilityGateway.UpdateEligibilityCheckStatus(checkData.Guid,
                                new EligibilityCheckStatusData { Status = CheckEligibilityStatus.error }, dbContext);

                            // Mark for deletion from queue
                            deleteQueueMessage = true;
                        }
                        else
                        {
                            // update message invisibility by 5 seocnds
                            try
                            {
                                await _storageQueueGateway.UpdateMessageAsync(item, queueName, 5);

                            }
                            catch (Exception queueUpdateException)
                            {
                                _logger.LogError(queueUpdateException, $"Item {i} - {checkData.Guid} - error updating queue item");
                            }
                        }
                    }

                    if (deleteQueueMessage)
                    {
                        try
                        {
                            await _storageQueueGateway.DeleteMessageAsync(item, queueName);
                        }
                        catch (Exception queueDeleteException)
                        {
                            _logger.LogError(queueDeleteException, $"Item {i} - {checkData.Guid} - error deleting queue item");
                        }
                    }

                    _logger.LogInformation($"Item {i} - {checkData.Guid} - processed in {sw.ElapsedMilliseconds}ms");
                }

            });

            await Task.WhenAll(tasks);

            // Iterate all checks and ensure that completed bulk checks are marked as complete if the last check has been processed
            var checks = retrievedItemsFromQueue.Select(x => JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(x.Body)));
            foreach (var check in checks)
            {
                await UpdateBulkCheckStatusIfCompleted(check.Guid);
            }

            st.Stop();
            _logger.LogInformation($"Queue {queueName.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} processed successfully in {st.ElapsedMilliseconds}ms");
        }
        return new MessageResponse { Data = "Queue Processed." };
    }


    private async Task UpdateBulkCheckStatusIfCompleted(string checkID)
    {
        await using var _db = _dbContextFactory.CreateDbContext();

        var bulkCheckId = await _db.CheckEligibilities
            .Where(x => x.EligibilityCheckID == checkID)
            .Select(x => x.BulkCheckID)
            .FirstOrDefaultAsync();

        // If bulkCheckId is null, it means this check isn't part of a bulk check, its single check, so we can skip the rest of the method
        if (bulkCheckId == null)
        {
            return;
        }

        // Check if there are any pending checks for the same bulk check ID
        _logger.LogInformation($"Checking for pending checks for bulk check ID: {bulkCheckId}");
        var countPending = await _db.CheckEligibilities
            .CountAsync(x =>
                x.BulkCheckID == bulkCheckId &&
                x.Status == CheckEligibilityStatus.queuedForProcessing);

        // If there are no queued checks, update the bulk check status to completed
        // as all checks have been processed
        if (countPending == 0)
        {
            _logger.LogInformation($"No pending checks found for bulk check ID: {bulkCheckId}");
            var bulkCheck = await _db.BulkChecks.FirstOrDefaultAsync(x => x.BulkCheckID == bulkCheckId);

            if (bulkCheck != null)
            {
                _logger.LogInformation($"Updating bulk check status to Completed for ID: {bulkCheckId}");

                bulkCheck.Status = BulkCheckStatus.Completed;
                bulkCheck.CompletedDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                var elapsedTime = bulkCheck.CompletedDate.Value - bulkCheck.SubmittedDate;

                var logEvent = JsonConvert.SerializeObject(new
                {
                    BulkCheckId = bulkCheck.BulkCheckID,
                    Status = bulkCheck.Status.ToString(),
                    SubmittedDate = bulkCheck.SubmittedDate,
                    CompletedDate = bulkCheck.CompletedDate,
                    ElapsedMilliseconds = elapsedTime.TotalMilliseconds,
                    NumberOfRecords = bulkCheck.NumberOfRecords,
                    OrganisationID = bulkCheck.OrganisationID
                });

                _logger.LogInformation("{BulkCheckEvent}", logEvent);
            }
        }
        else
        {
            _logger.LogInformation($"{countPending} pending checks found for bulk check ID: {bulkCheckId}. Bulk check status not be updated.");
        }
    }

}