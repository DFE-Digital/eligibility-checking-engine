using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            var loggerMock = new Mock<ILogger>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(factory => factory.CreateLogger(It.IsAny<string>()))
                .Returns(loggerMock.Object);

            var services = new ServiceCollection();
            services.AddHttpClient("Dwp")
                .ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object)

        .AddPolicyHandler((sp, msg) =>
        {
            var factory = sp.GetRequiredService<ILoggerFactory>();
            var logger = factory.CreateLogger("PollyRetryTests");
            return API.HttpClientPolicies.GetRetryPolicyWithJitter(logger, "DWP");
        })


                .AddPolicyHandler(API.HttpClientPolicies.GetCircuitBreakerPolicy());

            var provider = services.BuildServiceProvider();
            var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient("Dwp");

            // Act
            var response = await client.GetAsync("http://test");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(4, Is.EqualTo(callCount));
        }
    }
}
