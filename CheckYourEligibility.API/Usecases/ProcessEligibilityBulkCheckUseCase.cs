using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
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
    private readonly IStorageQueue _storageQueueGateway;
    private readonly IProcessEligibilityCheckUseCase _processEligibilityCheckUseCase;
    private readonly ILogger<ProcessEligibilityBulkCheckUseCase> _logger;

    public ProcessEligibilityBulkCheckUseCase(IStorageQueue storageQueueGateway, ILogger<ProcessEligibilityBulkCheckUseCase> logger,
        IProcessEligibilityCheckUseCase processEligibilityCheckUseCase)
    {
        _storageQueueGateway = storageQueueGateway;
        _processEligibilityCheckUseCase = processEligibilityCheckUseCase;
        _logger = logger;
    }

    public async Task<MessageResponse> Execute(string queueName)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            _logger.LogWarning("Empty queue name provided to ProcessQueueMessagesUseCase");
            return new MessageResponse { Data = "Invalid Request." };
        }

     var retrievedItemsFromQueue =  await _storageQueueGateway.ProcessQueueAsync(queueName);
        var sw = Stopwatch.StartNew();
        var st = Stopwatch.StartNew();
        int i = 0;
        if (retrievedItemsFromQueue.Count() > 0) {

            var tasks = retrievedItemsFromQueue.Select(async item =>
            {

                sw.Restart();
                var checkData = JsonConvert.DeserializeObject<QueueMessageCheck>(Encoding.UTF8.GetString(item.Body));
                try
                {
                    await _processEligibilityCheckUseCase.Execute(checkData.Guid);

                    i++;

                    Console.WriteLine(
                    $"Item_No....{i} \n" +
                    $"Process_Time....{sw.ElapsedMilliseconds:N0} ms \n" +
                    $"Time_Elapsed....{st.ElapsedMilliseconds:N0} ms");

                    _logger.LogInformation($"Reading queue item in {sw.ElapsedMilliseconds} ms");

                    await _storageQueueGateway.DeleteMessageAsync(item, queueName);

                }

                catch (NotFoundException ex)
                {
                    // Check not found in record - removing from queue
                    await _storageQueueGateway.DeleteMessageAsync(item, queueName);

                }
                catch (Exception ex)
                {
                    throw;
                }

            });
            await Task.WhenAll(tasks);

            st.Stop();

            _logger.LogInformation(
                $"Queue {queueName.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} processed successfully");
        

        }
        return new MessageResponse { Data = "Queue Processed." };

    }
}