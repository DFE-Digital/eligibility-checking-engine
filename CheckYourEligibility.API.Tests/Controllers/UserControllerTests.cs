using System.Security.Claims;
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class UserControllerTests : TestBase.TestBase
{
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ICreateOrUpdateFSMParentUserUseCase> _mockCreateOrUpdateUserUseCase;
    private ILogger<UserController> _mockLogger;
    private UserController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCreateOrUpdateUserUseCase = new Mock<ICreateOrUpdateFSMParentUserUseCase>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<UserController>>();
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new UserController(_mockLogger, _mockCreateOrUpdateUserUseCase.Object, _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockCreateOrUpdateUserUseCase.VerifyAll();
    }

    [Test]
    public async Task Given_valid_Request_Post_Should_Return_Status201Created()
    {
        // Arrange
        var request = _fixture.Create<UserCreateRequest>();

        var response = _fixture.Create<UserSaveItemResponse>();

        _mockCreateOrUpdateUserUseCase
            .Setup(x => x.Execute(It.IsAny<UserCreateRequest>()))
            .ReturnsAsync(response);

        var httpContext = new DefaultHttpContext();

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "FreeSchoolMealsAdmin:test@test.com")
                },
                "Test"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _sut.FsmParentUserPost(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = result as ObjectResult;

        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().BeEquivalentTo(response);

        _mockCreateOrUpdateUserUseCase.Verify(
            x => x.Execute(It.IsAny<UserCreateRequest>()),
            Times.Once);
    }

    [Test]
    public async Task Given_Null_Request_Should_Return_Status400BadRequest()
    {
        // Act
        var result = await _sut.FsmParentUserPost(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}