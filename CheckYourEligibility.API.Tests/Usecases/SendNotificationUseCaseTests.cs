using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
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
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Notification, request.Data.Email, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(request);

        // Assert
        _mockNotifyGateway.Verify(s => s.SendNotification(request), Times.Once);
        result.Should().BeOfType<NotificationResponse>();
    }

    [Test]
    public async Task Execute_Should_Create_Audit_Entry()
    {
        // Arrange
        var request = _fixture.Create<NotificationRequest>();
        var auditId = _fixture.Create<string>();

        _mockNotifyGateway.Setup(s => s.SendNotification(request));
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Notification, request.Data.Email, null))
            .ReturnsAsync(auditId);

        // Act
        await _sut.Execute(request);

        // Assert
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Notification, request.Data.Email, null), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Return_NotificationResponse()
    {
        // Arrange
        var request = _fixture.Create<NotificationRequest>();

        _mockNotifyGateway.Setup(s => s.SendNotification(request));
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Notification, request.Data.Email, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotificationResponse>();
    }
}