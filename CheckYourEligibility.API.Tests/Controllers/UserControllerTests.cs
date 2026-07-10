using System.Security.Claims;
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Enums;
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
    private Fixture _fixture;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ICreateOrUpdateUserUseCase> _mockCreateOrUpdateUserUseCase;
    private ILogger<UserController> _mockLogger;
    private UserController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCreateOrUpdateUserUseCase =
            new Mock<ICreateOrUpdateUserUseCase>(MockBehavior.Strict);

        _mockLogger = Mock.Of<ILogger<UserController>>();

        _mockAuditGateway =
            new Mock<IAudit>(MockBehavior.Strict);

        _sut = new UserController(
            _mockLogger,
            _mockCreateOrUpdateUserUseCase.Object,
            _mockAuditGateway.Object);

        SetupControllerWithUser();

        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockCreateOrUpdateUserUseCase.VerifyAll();
    }

    [Test]
    public async Task Given_Valid_Request_Should_Return_Status201Created()
    {
        // Arrange
        var request = new UserCreateRequest
        {
            Data = new UserData
            {
                Email = "test@test.com",
                Reference = "ABC123"
            }
        };

        var response = _fixture.Create<UserSaveItemResponse>();

        UserCreateRequest capturedRequest = null!;

        _mockCreateOrUpdateUserUseCase
            .Setup(x => x.Execute(It.IsAny<UserCreateRequest>()))
            .Callback<UserCreateRequest>(r => capturedRequest = r)
            .ReturnsAsync(response);

        // Act
        var result = await _sut.User(request);

        // Assert
        capturedRequest.Should().NotBeNull();

        capturedRequest.metaData.Should().NotBeNull();
        capturedRequest.metaData.Source.Should().Be("childcare-admin");
        capturedRequest.metaData.UserName.Should().Be("test-user");
        capturedRequest.metaData.OrganisationID.Should().Be(123);
        capturedRequest.metaData.OrganisationType.Should().Be("local-authority");

        result.Should().BeOfType<ObjectResult>();

        var objectResult = (ObjectResult)result;

        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Test]
    public async Task Given_Null_Data_Should_Return_BadRequest()
    {
        // Arrange
        var request = new UserCreateRequest
        {
            Data = null
        };

        // Act
        var result = await _sut.User(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        badRequest.Value.Should().BeOfType<ErrorResponse>();
    }

    [Test]
    public async Task Given_UseCase_Throws_UserSaveException_Should_Return_BadRequest()
    {
        // Arrange
        var request = new UserCreateRequest
        {
            Data = new UserData
            {
                Email = "test@test.com",
                Reference = "ABC123"
            }
        };

        _mockCreateOrUpdateUserUseCase
            .Setup(x => x.Execute(It.IsAny<UserCreateRequest>()))
            .ThrowsAsync(new UserSaveException("Failed to save user"));

        // Act
        var result = await _sut.User(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = (ErrorResponse)badRequest.Value!;

        errorResponse.Errors.First().Title
            .Should().Be("Failed to save user");
    }

    private void SetupControllerWithUser()
    {
        var httpContext = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                "childcare-admin:test-user"),

            new(
                "scope",
                "local_authority:123")
        };

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}