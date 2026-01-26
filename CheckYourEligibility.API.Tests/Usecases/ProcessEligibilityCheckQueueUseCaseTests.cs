using AutoFixture;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ProcessEligibilityCheckQueueUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IStorageQueue>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ProcessEligibilityBulkCheckUseCase>>(MockBehavior.Loose);
        _mockConf = new Mock<IConfiguration>(MockBehavior.Strict);
        _mockCheckEligibilityGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockDbContextFactory = new Mock<IDbContextFactory<EligibilityCheckContext>>(MockBehavior.Strict);
        _mockProcessEligibilityCheckUseCase = new Mock<IProcessEligibilityCheckUseCase>(MockBehavior.Strict);
        _sut = new ProcessEligibilityBulkCheckUseCase(_mockGateway.Object, _mockLogger.Object, _mockProcessEligibilityCheckUseCase.Object, _mockConf.Object, _mockCheckEligibilityGateway.Object, _mockDbContextFactory.Object);
      
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }
    private Mock<ICheckEligibility> _mockCheckEligibilityGateway;
    private Mock<IConfiguration> _mockConf;
    private Mock<IDbContextFactory<EligibilityCheckContext>> _mockDbContextFactory;
    private Mock<IStorageQueue> _mockGateway;
    private Mock<ILogger<ProcessEligibilityBulkCheckUseCase>> _mockLogger;
    private ProcessEligibilityBulkCheckUseCase _sut;
    private Mock<IProcessEligibilityCheckUseCase> _mockProcessEligibilityCheckUseCase;
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
        var queuedItems = new QueueMessage[0];
        _mockGateway.Setup(s => s.ProcessQueueAsync(queueName)).ReturnsAsync(queuedItems);
     
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
        var queuedItems = new QueueMessage[0];
        _mockGateway.Setup(s => s.ProcessQueueAsync(queueName)).ReturnsAsync(queuedItems);

        // Act
        var result = await _sut.Execute(queueName);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be("Queue Processed.");
    }
}