using AutoFixture;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ProcessEligibilityCheckQueueUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IStorageQueue>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ProcessEligibilityBulkCheckUseCase>>(MockBehavior.Loose);
        _sut = new ProcessEligibilityBulkCheckUseCase(_mockGateway.Object, _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    private Mock<IStorageQueue> _mockGateway;
    private Mock<ILogger<ProcessEligibilityBulkCheckUseCase>> _mockLogger;
    private ProcessEligibilityBulkCheckUseCase _sut;
    private Fixture _fixture;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_invalid_request_message_when_queue_is_null_or_empty(string queueName)
    {
        // Act
        var result = await _sut.Execute(queueName);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be("Invalid Request.");
    }

    [Test]
    public async Task Execute_calls_ProcessQueue_on_gateway_when_queue_name_is_valid()
    {
        // Arrange
        var queueName = _fixture.Create<string>();
        var queuedItemsuidList = _fixture.Create<List<string>>();
        _mockGateway.Setup(s => s.ProcessQueueAsync(queueName)).Returns(Task.FromResult(queuedItemsuidList));

        // Act
        await _sut.Execute(queueName);

        // Assert
        _mockGateway.Verify(s => s.ProcessQueueAsync(queueName), Times.Once);
    }

    [Test]
    public async Task Execute_returns_success_message_when_queue_processing_succeeds()
    {
        // Arrange
        var queueName = _fixture.Create<string>();
        var queuedItemsuidList = _fixture.Create<List<string>>();
        _mockGateway.Setup(s => s.ProcessQueueAsync(queueName)).Returns(Task.FromResult(queuedItemsuidList));

        // Act
        var result = await _sut.Execute(queueName);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be("Queue Processed.");
    }
}