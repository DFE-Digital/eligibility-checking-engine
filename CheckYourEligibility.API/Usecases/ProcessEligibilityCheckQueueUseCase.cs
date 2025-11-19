using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

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

public class ProcessEligibilityCheckQueueUseCase : IProcessEligibilityBulkCheckUseCase
{
    private readonly IStorageQueue _storageQueueGateway;
    private readonly ILogger<ProcessEligibilityCheckQueueUseCase> _logger;

    public ProcessEligibilityCheckQueueUseCase(IStorageQueue storageQueueGateway, ILogger<ProcessEligibilityCheckQueueUseCase> logger)
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

        await _storageQueueGateway.ProcessQueue(queue);
        _logger.LogInformation(
            $"Queue {queue.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} processed successfully");
        return new MessageResponse { Data = "Queue Processed." };
    }
}