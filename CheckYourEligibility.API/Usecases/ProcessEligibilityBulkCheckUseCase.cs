using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using System.Diagnostics;

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

    public ProcessEligibilityBulkCheckUseCase(IStorageQueue storageQueueGateway, ILogger<ProcessEligibilityBulkCheckUseCase> logger)
    {
        _storageQueueGateway = storageQueueGateway;
        _logger = logger;
    }

    public async Task<MessageResponse> Execute(string queue)
    {
        if (string.IsNullOrEmpty(queue))
        {
            _logger.LogWarning("Empty queue name provided to ProcessQueueMessagesUseCase");
            return new MessageResponse { Data = "Invalid Request." };
        }

     var queuedItemsGuidIds =  await _storageQueueGateway.ProcessQueueAsync(queue);
        var sw = Stopwatch.StartNew();
        var st = Stopwatch.StartNew();
        int i = 0;
        foreach (var queuedItemGuid in queuedItemsGuidIds) {
            sw.Restart();
            await _processEligibilityCheckUseCase.Execute(queuedItemGuid);
            i++;
            Console.WriteLine(
            $"Item_No....{i} \n" +
            $"Process_Time....{sw.ElapsedMilliseconds:N0} ms \n" +
            $"Time_Elapsed....{st.ElapsedMilliseconds:N0} ms");
        }

        st.Stop();

        _logger.LogInformation(
            $"Queue {queue.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} processed successfully");
        return new MessageResponse { Data = "Queue Processed." };
    }
}