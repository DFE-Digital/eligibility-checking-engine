using AutoFixture;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class SendNotificationUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockNotifyGateway = new Mock<INotify>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new SendNotificationUseCase(_mockNotifyGateway.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockNotifyGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<INotify> _mockNotifyGateway;
    private Mock<IAudit> _mockAuditGateway;
    private SendNotificationUseCase _sut;
    private Fixture _fixture;

    [Test]
    public async Task Execute_Should_Call_SendNotification_On_NotifyGateway()
    {
        // Arrange
        var request = _fixture.Create<NotificationRequest>();
        _mockNotifyGateway.Setup(s => s.SendNotification(request));

        // Act
        var result = await _sut.Execute(request);

        // Assert
        _mockNotifyGateway.Verify(s => s.SendNotification(request), Times.Once);
        result.Should().BeOfType<NotificationResponse>();
    }

}