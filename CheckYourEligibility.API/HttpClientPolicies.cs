using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace CheckYourEligibility.API
{
    public static class HttpClientPolicies
    {

        /// <summary>
        ///  Define Polly's policy for Http Retries with jitter.
        /// </summary>
        /// <param name="retryCount">How many times a single request will be retried before giving up</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicyWithJitter(ILogger logger)
        {
            var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(2), retryCount: 3);

            return HttpPolicyExtensions
                .HandleTransientHttpError() //(e.g., a momentary 5xx, a brief network hiccup).
                .WaitAndRetryAsync(delay,
                    onRetryAsync: async (outcome, timespan, retryAttempt, context) =>
                    {
                        logger.LogWarning(
                            "Retry attempt {RetryAttempt} will wait {Delay}s. Reason: {ExceptionMessage}",
                            retryAttempt,
                            timespan.TotalSeconds,
                            outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);

                        await Task.CompletedTask;
                    });
            }

        /// <summary>
        /// Define a cicruit breaker policy
        /// Circuit breaks when 10% of calls 
        /// failsamplingDuration: 30s Looks at request outcomes over the last 30 seconds minimumThroughput: 100
        /// Needs at least 100 calls in the sampling window to make a decision 
        /// durationOfBreak: 5s When broken, stays open (fails fast) for 5 seconds
        /// </summary>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {

            return HttpPolicyExtensions
                .HandleTransientHttpError() //(e.g., a momentary 5xx, a brief network hiccup).
                .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.1,
                samplingDuration: TimeSpan.FromSeconds(30),
                minimumThroughput: 100,
                durationOfBreak: TimeSpan.FromSeconds(5));
        }
    }
}
