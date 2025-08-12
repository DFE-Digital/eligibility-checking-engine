using System.Security.Claims;
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Writers;
using Moq;
using NUnit.Framework.Constraints;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class RateLimitUseCaseTests
{
    private Mock<IRateLimit> _mockRateLimitGateway = null!;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor = null!;
    private CreateRateLimitEventUseCase _sut = null!;
    private Fixture _fixture = null!;


    [SetUp]
    public void Setup()
    {
        _mockRateLimitGateway = new Mock<IRateLimit>(MockBehavior.Strict);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        _sut = new CreateRateLimitEventUseCase(_mockRateLimitGateway.Object, _mockHttpContextAccessor.Object);
        _fixture = new Fixture();
    }

    [Test]
    public async Task Execute_Should_Allow_Request_When_Size_LessThan_Capacity()
    {
        // Arrange
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();

        _mockRateLimitGateway.Setup(s => s.Create(It.IsAny<RateLimitEvent>())).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.UpdateStatus(It.IsAny<string>(), true)).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength))
            .Returns(Task.FromResult(1));

        var context = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim("scope", "local_authority:894")
        };
        context.User.AddIdentity(new ClaimsIdentity(claims));
        _mockHttpContextAccessor.Setup(s => s.HttpContext).Returns(context);

        // Act
        var result = await _sut.Execute(options);

        // Assert
        result.Should().Be(true);
        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Once);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Once);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), true), Times.Once);
    }
}