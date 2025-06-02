using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ValidationException = FluentValidation.ValidationException;

namespace CheckYourEligibility.API.Tests;

public class ApplicationControllerTests : TestBase.TestBase
{
    private IConfigurationRoot _configuration;
    private Fixture _fixture;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ICreateApplicationUseCase> _mockCreateApplicationUseCase;
    private Mock<IGetApplicationUseCase> _mockGetApplicationUseCase;
    private ILogger<ApplicationController> _mockLogger;
    private Mock<ISearchApplicationsUseCase> _mockSearchApplicationsUseCase;
    private Mock<IUpdateApplicationStatusUseCase> _mockUpdateApplicationStatusUseCase;
    private ApplicationController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCreateApplicationUseCase = new Mock<ICreateApplicationUseCase>(MockBehavior.Strict);
        _mockGetApplicationUseCase = new Mock<IGetApplicationUseCase>(MockBehavior.Strict);
        _mockSearchApplicationsUseCase = new Mock<ISearchApplicationsUseCase>(MockBehavior.Strict);
        _mockUpdateApplicationStatusUseCase = new Mock<IUpdateApplicationStatusUseCase>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<ApplicationController>>();

        // config data for Jwt:Scopes:local_authority
        var configData = new Dictionary<string, string>
        {
            { "Jwt:Scopes:local_authority", "local_authority" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new ApplicationController(
            _mockLogger,
            _configuration,
            _mockCreateApplicationUseCase.Object,
            _mockGetApplicationUseCase.Object,
            _mockSearchApplicationsUseCase.Object,
            _mockUpdateApplicationStatusUseCase.Object,
            _mockAuditGateway.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockCreateApplicationUseCase.VerifyAll();
        _mockGetApplicationUseCase.VerifyAll();
        _mockSearchApplicationsUseCase.VerifyAll();
        _mockUpdateApplicationStatusUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }
    [Test]
    public async Task Given_valid_NInumber_ApplicationRequest_Post_Should_Return_Status201Created()
    {
        // Arrange
        var request = _fixture.Create<ApplicationRequest>();
        var applicationFsm = _fixture.Create<ApplicationSaveItemResponse>();
        var localAuthorityIds = new List<int> { 1 };

        // Mock ClaimsPrincipal extension method
        _mockCreateApplicationUseCase.Setup(cs => cs.Execute(request, localAuthorityIds)).ReturnsAsync(applicationFsm);

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        var expectedResult = new ObjectResult(applicationFsm)
        { StatusCode = StatusCodes.Status201Created };

        // Act
        var response = await _sut.Application(request);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }
    private void SetupControllerWithLocalAuthorityIds(List<int> localAuthorityIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>();

        // Add appropriate scope claims based on localAuthorityIds
        if (localAuthorityIds.Contains(0))
        {
            claims.Add(new Claim("scope", "local_authority"));
        }
        else
        {
            var scopeValue = string.Join(" ", localAuthorityIds.Select(id => $"local_authority:{id}"));
            claims.Add(new Claim("scope", scopeValue));
        }

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }
    [Test]
    public async Task Given_InValidRequest_Values_Application_Should_Return_Status400BadRequest()
    {
        // Arrange
        var request = new ApplicationRequest();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockCreateApplicationUseCase.Setup(cs => cs.Execute(request, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Invalid request, data is required"));

        // Act
        var response = await _sut.Application(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
    }
    [Test]
    public async Task Given_InValidRequest_Validation_Application_Should_Return_Status400BadRequest()
    {
        // Arrange
        var request = _fixture.Create<ApplicationRequest>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        if (request.Data != null)
        {
            request.Data.ParentLastName = string.Empty;
        }

        // Setup mock to throw ValidationException when called with this request
        _mockCreateApplicationUseCase.Setup(cs => cs.Execute(request, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Parent last name cannot be empty"));

        // Act
        var response = await _sut.Application(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }
    [Test]
    public async Task Given_InValidRequest_Type_Application_Should_Return_Status400BadRequest()
    {
        // Arrange
        var request = _fixture.Create<ApplicationRequest>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        if (request.Data != null)
        {
            request.Data.Type = CheckEligibilityType.None;
        }

        // Setup mock to throw ValidationException when called with this request
        _mockCreateApplicationUseCase.Setup(cs => cs.Execute(request, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Invalid request, Valid Type is required: None"));

        // Act
        var response = await _sut.Application(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task Given_InValid_guid_Application_Should_Return_StatusNotFound()
    {
        // Arrange
        var guid = _fixture.Create<string>();

        _mockGetApplicationUseCase.Setup(cs => cs.Execute(guid))
            .ThrowsAsync(new NotFoundException());
        var expectedResult = new NotFoundObjectResult(new ErrorResponse { Errors = [new Error { Title = guid }] });

        // Act
        var response = await _sut.Application(guid);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_Valid_guid_Application_Should_Return_StatusOk()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var expectedResponse = _fixture.Create<ApplicationItemResponse>();
        _mockGetApplicationUseCase.Setup(cs => cs.Execute(guid)).ReturnsAsync(expectedResponse);
        var expectedResult = new ObjectResult(expectedResponse)
        { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.Application(guid);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }
    [Test]
    public async Task Given_Valid_ApplicationSearch_Should_Return_StatusOk()
    {
        // Arrange
        var model = _fixture.Create<ApplicationRequestSearch>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Set the LocalAuthority in the model to match our authorized LocalAuthority
        if (model.Data == null)
        {
            model.Data = new ApplicationRequestSearchData();
        }
        model.Data.LocalAuthority = 1;  // Match the localAuthorityIds we set up
        var expectedResponse = _fixture.Create<ApplicationSearchResponse>();
        _mockSearchApplicationsUseCase.Setup(cs => cs.Execute(model, It.IsAny<string>())).ReturnsAsync(expectedResponse);
        var expectedResult = new ObjectResult(expectedResponse)
        { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }
    [Test]
    public async Task Given_InValid_guid_ApplicationStatusUpdate_Should_Return_StatusNotFound()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();
        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request))
            .ReturnsAsync((ApplicationStatusUpdateResponse)null);
        var expectedResult = new NotFoundObjectResult(new ErrorResponse { Errors = [new Error { Title = "" }] });

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_Valid_guid_ApplicationStatusUpdate_Should_Return_StatusOk()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();
        var expectedResponse = _fixture.Create<ApplicationStatusUpdateResponse>();
        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request)).ReturnsAsync(expectedResponse);
        var expectedResult = new ObjectResult(expectedResponse)
        { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_ApplicationSearch_Without_LocalAuthority_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationRequestSearch>();

        // Setup controller with empty local authority claims
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task Given_ApplicationSearch_With_NonMatching_LocalAuthority_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationRequestSearch>();
        if (model.Data == null)
        {
            model.Data = new ApplicationRequestSearchData();
        }
        model.Data.LocalAuthority = 5; // A different local authority than we'll authorize

        // Setup controller with specific local authority claims (not including 5)
        SetupControllerWithLocalAuthorityIds(new List<int> { 1, 2, 3 });

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("Local authority scope does not match requested LocalAuthority");
    }
}