namespace CheckYourEligibility.API.Infrastructure
{
    public class ManagedBulkInsertOptions
    {
        public int BatchSize { get; set; } = 1000;
        public int TimeoutSeconds { get; set; } = 120;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 2;
    }
}