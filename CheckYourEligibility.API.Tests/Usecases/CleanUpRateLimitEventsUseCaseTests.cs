using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CleanUpRateLimitEventsChecksUseCaseTests : TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IRateLimit>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new CleanUpRateLimitEventsUseCase(_mockGateway.Object, _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IRateLimit> _mockGateway;
    private Mock<IAudit> _mockAuditGateway;
    private CleanUpRateLimitEventsUseCase _sut;

    [Test]
    public async Task Execute_Should_Call_CleanUpRateLimitEvents_On_gateway()
    {
        // Arrange
        _mockGateway.Setup(s => s.CleanUpRateLimitEvents()).Returns(Task.CompletedTask);
        // Act
        await _sut.Execute();

        // Assert
        _mockGateway.Verify(s => s.CleanUpRateLimitEvents(), Times.Once);
    }
}