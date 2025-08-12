using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework.Constraints;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class RateLimitUseCaseTests
{
    private Mock<IRateLimit> _mockRateLimitGateway = null!;
    private CreateRateLimitEventUseCase _sut = null!;
    private Fixture _fixture = null!;


    [SetUp]
    public void Setup()
    {
        _mockRateLimitGateway = new Mock<IRateLimit>(MockBehavior.Strict);
        _sut = new CreateRateLimitEventUseCase(_mockRateLimitGateway.Object);
        _fixture = new Fixture();
    }

    [Test]
    public async Task Execute_Should_Allow_Request_When_Size_LessThan_Capacity()
    {
        /*
        // Arrange
        var httpContext = _fixture.Create<HttpContext>();
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();

        _mockRateLimitGateway.Setup(s => s.Create(It.IsAny<RateLimitEvent>())).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.UpdateStatus(It.IsAny<string>(), true)).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength))
            .Returns(Task.FromResult(1));

        // Act
        var result = await _sut.Execute(httpContext, options);

        // Assert
        result.Should().Be(true);
        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Once);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Once);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), true), Times.Once);
        */
    }
}