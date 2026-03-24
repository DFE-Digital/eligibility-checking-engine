using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Sockets;

namespace CheckYourEligibility.API.Tests.HttpClientPolicies
{
    public class HttpClientPoliciesTests

    {

        [Test]
        public async Task HttpClient_Retries_OnTransientFailure()
        {
            // Arrange: Mock handler to fail twice, then succeed
            var handlerMock = new Mock<HttpMessageHandler>();
            int callCount = 0;
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount < 4)
                    {
                        throw new HttpRequestException();
                    }
                        return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var services = new ServiceCollection();
            services.AddHttpClient("Dwp")
                .ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object)
                .AddPolicyHandler(API.HttpClientPolicies.GetRetryPolicyWithJitter())
                .AddPolicyHandler(API.HttpClientPolicies.GetCircuitBreakerPolicy());

            var provider = services.BuildServiceProvider();
            var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient("Dwp");

            // Act
            var response = await client.GetAsync("http://test");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(4,  Is.EqualTo(callCount));
        }
        [Test]
        public async Task HttpClient_Retries_OnTaskCancellationError()
        {
            // Arrange: Mock handler to fail twice, then succeed
            var handlerMock = new Mock<HttpMessageHandler>();
            int callCount = 0;
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount < 4)
                    {
                        throw CreateHttpClientTimeoutLikeException();
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var services = new ServiceCollection();
            services.AddHttpClient("Dwp")
                .ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object)
                .AddPolicyHandler(API.HttpClientPolicies.GetRetryPolicyWithJitter())
                .AddPolicyHandler(API.HttpClientPolicies.GetCircuitBreakerPolicy());

            var provider = services.BuildServiceProvider();
            var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient("Dwp");

            // Act
            var response = await client.GetAsync("http://test");
            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(4, Is.EqualTo(callCount)); // Should retry 3 times before succeedding
        }

        private static Exception CreateHttpClientTimeoutLikeException()
{
    // Inner
    var socket = new SocketException((int)SocketError.OperationAborted);

    // Wrapped in
    var io = new IOException(
        "Unable to read data from the transport connection: " +
        "The I/O operation has been aborted because of either a thread exit or an application request.",
        socket);

    var innerTce = new TaskCanceledException("The operation was canceled.");

    var timeout = new TimeoutException("The operation was canceled.", io);

    // Top-level: HttpClient throws TaskCanceledException on timeout
    return new TaskCanceledException(
        "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing. " +
        "The operation was canceled. The operation was canceled. " +
        "Unable to read data from the transport connection: " +
        "The I/O operation has been aborted because of either a thread exit or an application request.. " +
        "The I/O operation has been aborted because of either a thread exit or an application request.",
        timeout);
}

    }
}
