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
    private Fixture _fixture;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ICreateOrUpdateUserUseCase> _mockCreateOrUpdateUserUseCase;
    private ILogger<UserController> _mockLogger;
    private UserController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCreateOrUpdateUserUseCase = new Mock<ICreateOrUpdateUserUseCase>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<UserController>>();
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new UserController(_mockLogger, _mockCreateOrUpdateUserUseCase.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();
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
        _mockCreateOrUpdateUserUseCase.Setup(cs => cs.Execute(request)).ReturnsAsync(response);

        var expectedResult = new ObjectResult(response)
            { StatusCode = StatusCodes.Status201Created };

        // Act
        var result = await _sut.User(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_InValidRequest_Should_Return_Status400BadRequest()
    {
        // Arrange
        var request = new UserCreateRequest();

        // Act
        var result = await _sut.User(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}