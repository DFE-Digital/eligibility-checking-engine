using System.Security.Claims;
using System.Text;
using AutoFixture;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

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

    [TearDown]
    public void Teardown()
    {
        _mockRateLimitGateway.VerifyAll();
        _mockHttpContextAccessor.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Allow_Request_When_Size_LessThan_Capacity()
    {
        // Arrange
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();
        options.PermitLimit = 10;

        _mockRateLimitGateway.Setup(s => s.Create(It.IsAny<RateLimitEvent>())).Returns(Task.CompletedTask);
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
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status429TooManyRequests);

        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Once);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Once);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), true), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Deny_Request_When_Size_MoreThan_Capacity()
    {
        // Arrange
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();
        options.PermitLimit = 1;

        _mockRateLimitGateway.Setup(s => s.Create(It.IsAny<RateLimitEvent>())).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.UpdateStatus(It.IsAny<string>(), false)).Returns(Task.CompletedTask);
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
        result.Should().Be(false);
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Once);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Once);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), false), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Request_When_Size_EqualTo_Capacity()
    {
        // Arrange
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();
        options.PermitLimit = 1;

        _mockRateLimitGateway.Setup(s => s.Create(It.IsAny<RateLimitEvent>())).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength))
            .Returns(Task.FromResult(0));

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
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status429TooManyRequests);

        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Once);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Once);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), true), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Deny_Bulk_Request_When_Size_MoreThan_Capacity()
    {
        // Arrange
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();
        options.PermitLimit = 5;

        _mockRateLimitGateway.Setup(s => s.Create(It.IsAny<RateLimitEvent>())).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.UpdateStatus(It.IsAny<string>(), false)).Returns(Task.CompletedTask);
        _mockRateLimitGateway.Setup(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength))
            .Returns(Task.FromResult(1));

        var context = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim("scope", "local_authority:894")
        };
        context.User.AddIdentity(new ClaimsIdentity(claims));
        var newObjectString = "{\"data\": [1, 2, 3, 4, 5]}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(newObjectString));
        context.Request.Path = "/bulk-check";
        _mockHttpContextAccessor.Setup(s => s.HttpContext).Returns(context);

        // Act
        var result = await _sut.Execute(options);

        // Assert
        result.Should().Be(false);
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Once);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Once);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), false), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Request_When_No_LA_Id()
    {
        // Arrange
        var options = _fixture.Create<RateLimiterMiddlewareOptions>();
        options.PermitLimit = 1;

        var context = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim("scope", "local_authority")
        };
        context.User.AddIdentity(new ClaimsIdentity(claims));
        _mockHttpContextAccessor.Setup(s => s.HttpContext).Returns(context);

        // Act
        var result = await _sut.Execute(options);

        // Assert
        result.Should().Be(true);
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status429TooManyRequests);
        
        _mockRateLimitGateway.Verify(s => s.Create(It.IsAny<RateLimitEvent>()), Times.Never);
        _mockRateLimitGateway.Verify(s => s.GetQueriesInWindow(It.IsAny<string>(), It.IsAny<DateTime>(), options.WindowLength), Times.Never);
        _mockRateLimitGateway.Verify(s => s.UpdateStatus(It.IsAny<string>(), true), Times.Never);
    }
}