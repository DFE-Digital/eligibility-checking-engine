using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class NotificationControllerTests : TestBase.TestBase
{
    private Fixture _fixture;
    private Mock<IAudit> _mockAuditGateway;
    private ILogger<NotificationController> _mockLogger;
    private Mock<ISendNotificationUseCase> _mockSendNotificationUseCase;
    private NotificationController _sut;

    [SetUp]
    public void Setup()
    {
        _mockSendNotificationUseCase = new Mock<ISendNotificationUseCase>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<NotificationController>>();

        _sut = new NotificationController(
            _mockLogger,
            _mockSendNotificationUseCase.Object,
            _mockAuditGateway.Object);

        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockSendNotificationUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    [Test]
    public async Task Given_Valid_NotificationRequest_Should_Return_Status201Created()
    {
        // Arrange
        var request = _fixture.Create<NotificationRequest>();
        var notificationResponse = _fixture.Create<NotificationResponse>();
        _mockSendNotificationUseCase.Setup(cs => cs.Execute(request)).ReturnsAsync(notificationResponse);

        var expectedResult = new ObjectResult(notificationResponse)
            { StatusCode = StatusCodes.Status201Created };

        // Act
        var response = await _sut.Notification(request);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_Exception_Occurs_Should_Return_Status400BadRequest()
    {
        // Arrange
        var request = _fixture.Create<NotificationRequest>();
        _mockSendNotificationUseCase.Setup(cs => cs.Execute(request))
            .ThrowsAsync(new Exception("An error occurred"));

        // Act
        var response = await _sut.Notification(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult.Value as ErrorResponse;
        errorResponse.Errors.Should().HaveCount(1);
    }

    [Test]
    public async Task Given_ValidationException_Should_Return_Status400BadRequest()
    {
        // Arrange
        var request = _fixture.Create<NotificationRequest>();
        _mockSendNotificationUseCase.Setup(cs => cs.Execute(request))
            .ThrowsAsync(new ValidationException("Validation failed"));

        // Act
        var response = await _sut.Notification(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
    }
}