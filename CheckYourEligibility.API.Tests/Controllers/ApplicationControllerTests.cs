using System.Security.Claims;
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
using ValidationException = FluentValidation.ValidationException;

namespace CheckYourEligibility.API.Tests;

public class ApplicationControllerTests : TestBase.TestBase
{
    private IConfigurationRoot _configuration = null!;
    private new Fixture _fixture = null!; // Added 'new' keyword
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ICreateApplicationUseCase> _mockCreateApplicationUseCase = null!;
    private Mock<IGetApplicationUseCase> _mockGetApplicationUseCase = null!;
    private ILogger<ApplicationController> _mockLogger = null!;
    private Mock<ISearchApplicationsUseCase> _mockSearchApplicationsUseCase = null!;
    private Mock<IUpdateApplicationStatusUseCase> _mockUpdateApplicationStatusUseCase = null!;
    private Mock<IImportApplicationsUseCase> _mockImportApplicationsUseCase = null!; 
    private Mock<IDeleteApplicationUseCase> _mockDeleteApplicationUseCase = null!; 
    private ApplicationController _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockCreateApplicationUseCase = new Mock<ICreateApplicationUseCase>(MockBehavior.Strict);
        _mockGetApplicationUseCase = new Mock<IGetApplicationUseCase>(MockBehavior.Strict);
        _mockSearchApplicationsUseCase = new Mock<ISearchApplicationsUseCase>(MockBehavior.Strict);
        _mockUpdateApplicationStatusUseCase = new Mock<IUpdateApplicationStatusUseCase>(MockBehavior.Strict);
        _mockImportApplicationsUseCase = new Mock<IImportApplicationsUseCase>(MockBehavior.Strict); 
        _mockDeleteApplicationUseCase = new Mock<IDeleteApplicationUseCase>(MockBehavior.Strict); 
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<ApplicationController>>();
        _fixture = new Fixture(); // Ensure _fixture is initialized

        // config data for Jwt:Scopes:local_authority
        var configData = new Dictionary<string, string?> // Changed to string? for value
        {
            { "Jwt:Scopes:local_authority", "local_authority" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData) // Now matches expected type
            .Build();

        _sut = new ApplicationController(
            _mockLogger,
            _configuration,
            _mockCreateApplicationUseCase.Object,
            _mockGetApplicationUseCase.Object,
            _mockSearchApplicationsUseCase.Object,
            _mockUpdateApplicationStatusUseCase.Object,
            _mockImportApplicationsUseCase.Object, 
            _mockDeleteApplicationUseCase.Object,
            _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockCreateApplicationUseCase.VerifyAll();
        _mockGetApplicationUseCase.VerifyAll();
        _mockSearchApplicationsUseCase.VerifyAll();
        _mockUpdateApplicationStatusUseCase.VerifyAll();
        _mockImportApplicationsUseCase.VerifyAll();
        _mockDeleteApplicationUseCase.VerifyAll();
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
        var claims = SetupSpecificScopeIdClaims(localAuthorityIds, "local_authority");

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupControllerWithLaAndMatIds(List<int> localAuthorityIds, List<int> multiAcademyTrustIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = SetupSpecificScopeIdClaims(localAuthorityIds, "local_authority");
        claims.AddRange(SetupSpecificScopeIdClaims(multiAcademyTrustIds, "multi_academy_trust"));

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private List<Claim> SetupSpecificScopeIdClaims(List<int> ids, string scopeName)
    {
        var claims = new List<Claim>();

        // Add appropriate scope claims based on ids
        if (ids.Contains(0))
        {
            claims.Add(new Claim("scope", scopeName));
        }
        else
        {
            var scopeValue = string.Join(" ", ids.Select(id => $"{scopeName}:{id}"));
            claims.Add(new Claim("scope", scopeValue));
        }

        return claims;
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
        badRequestResult!.Value.Should().BeOfType<ErrorResponse>(); // Added null-forgiving operator
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
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockGetApplicationUseCase.Setup(cs => cs.Execute(guid, localAuthorityIds))
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
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockGetApplicationUseCase.Setup(cs => cs.Execute(guid, localAuthorityIds)).ReturnsAsync(expectedResponse);
        var expectedResult = new ObjectResult(expectedResponse)
            { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.Application(guid);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_Valid_ApplicationSearch_LA_Scope_Should_Return_StatusOk()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();
        var localAuthorityIds = new List<int> { 1 };
        var multiAcademyTrustIds = new List<int> { };
        var establishmentIds = new List<int> { };

        // Setup controller with local authority claims
        SetupControllerWithLaAndMatIds(localAuthorityIds, multiAcademyTrustIds);

        // Set the LocalAuthority in the model to match our authorized LocalAuthority
        if (model.Data == null)
        {
            model.Data = new ApplicationSearchRequestData();
        }

        model.Data.LocalAuthority = 1; // Match the localAuthorityIds we set up
        var expectedResponse = _fixture.Create<ApplicationSearchResponse>();
        _mockSearchApplicationsUseCase.Setup(cs => cs.Execute(model, localAuthorityIds, multiAcademyTrustIds, establishmentIds)).ReturnsAsync(expectedResponse);
        var expectedResult = new ObjectResult(expectedResponse)
            { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_Valid_ApplicationSearch_MAT_Scope_Should_Return_StatusOk()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();
        var localAuthorityIds = new List<int> { };
        var multiAcademyTrustIds = new List<int> { 1 };
        var establishmentIds = new List<int> { };

        // Setup controller with local authority claims
        SetupControllerWithLaAndMatIds(localAuthorityIds, multiAcademyTrustIds);

        // Set the LocalAuthority in the model to match our authorized LocalAuthority
        if (model.Data == null)
        {
            model.Data = new ApplicationSearchRequestData();
        }

        model.Data.LocalAuthority = 1; // Match the localAuthorityIds we set up
        var expectedResponse = _fixture.Create<ApplicationSearchResponse>();
        _mockSearchApplicationsUseCase.Setup(cs => cs.Execute(model, localAuthorityIds, multiAcademyTrustIds, establishmentIds)).ReturnsAsync(expectedResponse);
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
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request, localAuthorityIds))
            .ReturnsAsync((ApplicationStatusUpdateResponse)null!);
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
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request, localAuthorityIds))
            .ReturnsAsync(expectedResponse);
        var expectedResult = new ObjectResult(expectedResponse)
            { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_ApplicationSearch_Without_LA_Or_MAT_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();

        // Setup controller with empty local authority claims
        SetupControllerWithLaAndMatIds(new List<int>(), new List<int>());

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("No local authority or multi academy trust scope found");
    }

    [Test]
    public async Task Given_ApplicationSearch_With_NonMatching_LocalAuthority_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();
        if (model.Data == null)
        {
            model.Data = new ApplicationSearchRequestData();
        }

        model.Data.LocalAuthority = 5; // A different local authority than we'll authorize

        // Setup controller with specific local authority claims (not including 5)
        SetupControllerWithLocalAuthorityIds(new List<int> { 1, 2, 3 });

        // Setup mock to throw UnauthorizedAccessException for unauthorized access
        _mockSearchApplicationsUseCase
            .Setup(cs => cs.Execute(It.IsAny<ApplicationSearchRequest>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<List<int>>()))
            .ThrowsAsync(
                new UnauthorizedAccessException("Local authority scope does not match requested LocalAuthority"));

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should()
            .Be("Local authority scope does not match requested LocalAuthority");
    }

    [Test]
    public async Task Given_ApplicationStatusUpdate_Without_LocalAuthority_Should_Return_BadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();

        // Setup controller with empty local authority claims
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task Given_ApplicationStatusUpdate_With_NotFoundException_Should_Return_StatusNotFound()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request, localAuthorityIds))
            .ThrowsAsync(new NotFoundException("Application not found"));

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = response as NotFoundObjectResult;
        notFoundResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = notFoundResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("Application not found");
    }

    [Test]
    public async Task Given_ApplicationStatusUpdate_With_UnauthorizedAccessException_Should_Return_BadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request, localAuthorityIds))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("Access denied");
    }

    [Test]
    public async Task Given_ApplicationStatusUpdate_With_ValidationException_Should_Return_BadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockUpdateApplicationStatusUseCase.Setup(cs => cs.Execute(guid, request, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Invalid status"));

        // Act
        var response = await _sut.ApplicationStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("Invalid status");
    }

    [Test]
    public async Task Given_Application_Without_LocalAuthority_Should_Return_BadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();

        // Setup controller with empty local authority claims
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.Application(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task Given_Application_With_UnauthorizedAccessException_Should_Return_BadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 1 };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockGetApplicationUseCase.Setup(cs => cs.Execute(guid, localAuthorityIds))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        var response = await _sut.Application(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult?.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult?.Value as ErrorResponse;
        errorResponse?.Errors?.FirstOrDefault()?.Title.Should().Be("Access denied");
    }

    [Test]
    public async Task Given_ApplicationSearch_With_ArgumentException_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();
        var localAuthorityIds = new List<int> { 1 };
        var multiAcademyTrustIds = new List<int> { };
        var establishmentIds = new List<int> { };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockSearchApplicationsUseCase.Setup(cs => cs.Execute(model, localAuthorityIds, multiAcademyTrustIds, establishmentIds))
            .ThrowsAsync(new ArgumentException("Invalid request, data is required"));

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult!.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult.Value as ErrorResponse;
        errorResponse!.Errors!.FirstOrDefault()?.Title.Should().Be("Invalid request, data is required");
    }

    [Test]
    public async Task Given_ApplicationSearch_With_UnauthorizedAccessException_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();
        var localAuthorityIds = new List<int> { 1 };
        var multiAcademyTrustIds = new List<int> { };
        var establishmentIds = new List<int> { };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockSearchApplicationsUseCase.Setup(cs => cs.Execute(model, localAuthorityIds, multiAcademyTrustIds, establishmentIds))
            .ThrowsAsync(
                new UnauthorizedAccessException(
                    "You do not have permission to search applications for this local authority"));

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult!.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult.Value as ErrorResponse;
        errorResponse!.Errors!.FirstOrDefault()?.Title.Should()
            .Be("You do not have permission to search applications for this local authority");
    }

    [Test]
    public async Task Given_ApplicationSearch_With_GeneralException_Should_Return_BadRequest()
    {
        // Arrange
        var model = _fixture.Create<ApplicationSearchRequest>();
        var localAuthorityIds = new List<int> { 1 };
        var multiAcademyTrustIds = new List<int> { };
        var establishmentIds = new List<int> { };

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockSearchApplicationsUseCase.Setup(cs => cs.Execute(model, localAuthorityIds, multiAcademyTrustIds, establishmentIds))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var response = await _sut.ApplicationSearch(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = response as BadRequestObjectResult;
        badRequestResult!.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = badRequestResult.Value as ErrorResponse;
        errorResponse!.Errors!.FirstOrDefault()?.Title.Should().Be("Database connection failed");
    }

    [Test]
    public async Task BulkImportApplications_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ApplicationBulkImportRequest
            { File = new FormFile(new MemoryStream(), 0, 0, "file", "file.csv") };
        var expectedResponse = _fixture.Create<ApplicationBulkImportResponse>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.Execute(request, localAuthorityIds)).ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.BulkImportApplications(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Test]
    public async Task BulkImportApplications_NoLocalAuthorityScope_ReturnsBadRequest()
    {
        // Arrange
        var request = new ApplicationBulkImportRequest
            { File = new FormFile(new MemoryStream(), 0, 0, "file", "file.csv") };
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var result = await _sut.BulkImportApplications(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task BulkImportApplications_ValidationException_ReturnsBadRequest()
    {
        // Arrange
        var request = new ApplicationBulkImportRequest
            { File = new FormFile(new MemoryStream(), 0, 0, "file", "file.csv") };
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.Execute(request, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var result = await _sut.BulkImportApplications(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task BulkImportApplications_UnauthorizedAccessException_ReturnsBadRequest()
    {
        // Arrange
        var request = new ApplicationBulkImportRequest
            { File = new FormFile(new MemoryStream(), 0, 0, "file", "file.csv") };
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.Execute(request, localAuthorityIds))
            .ThrowsAsync(new UnauthorizedAccessException("Unauthorized"));

        // Act
        var result = await _sut.BulkImportApplications(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("Unauthorized");
    }

    [Test]
    public async Task BulkImportApplications_GeneralException_ReturnsBadRequest()
    {
        // Arrange
        var request = new ApplicationBulkImportRequest
            { File = new FormFile(new MemoryStream(), 0, 0, "file", "file.csv") };
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.Execute(request, localAuthorityIds))
            .ThrowsAsync(new Exception("General error"));

        // Act
        var result = await _sut.BulkImportApplications(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("General error");
    }

    [Test]
    public async Task BulkImportApplicationsFromJson_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = _fixture.Create<ApplicationBulkImportJsonRequest>();
        var expectedResponse = _fixture.Create<ApplicationBulkImportResponse>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.ExecuteFromJson(request, localAuthorityIds))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.BulkImportApplicationsFromJson(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Test]
    public async Task BulkImportApplicationsFromJson_NoLocalAuthorityScope_ReturnsBadRequest()
    {
        // Arrange
        var request = _fixture.Create<ApplicationBulkImportJsonRequest>();
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var result = await _sut.BulkImportApplicationsFromJson(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task BulkImportApplicationsFromJson_ValidationException_ReturnsBadRequest()
    {
        // Arrange
        var request = _fixture.Create<ApplicationBulkImportJsonRequest>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.ExecuteFromJson(request, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var result = await _sut.BulkImportApplicationsFromJson(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task BulkImportApplicationsFromJson_UnauthorizedAccessException_ReturnsBadRequest()
    {
        // Arrange
        var request = _fixture.Create<ApplicationBulkImportJsonRequest>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.ExecuteFromJson(request, localAuthorityIds))
            .ThrowsAsync(new UnauthorizedAccessException("Unauthorized"));

        // Act
        var result = await _sut.BulkImportApplicationsFromJson(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("Unauthorized");
    }

    [Test]
    public async Task BulkImportApplicationsFromJson_GeneralException_ReturnsBadRequest()
    {
        // Arrange
        var request = _fixture.Create<ApplicationBulkImportJsonRequest>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockImportApplicationsUseCase.Setup(x => x.ExecuteFromJson(request, localAuthorityIds))
            .ThrowsAsync(new Exception("General error"));

        // Act
        var result = await _sut.BulkImportApplicationsFromJson(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("General error");
    }

    [Test]
    public async Task DeleteApplication_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockDeleteApplicationUseCase.Setup(x => x.Execute(guid, localAuthorityIds)).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteApplication(guid);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task DeleteApplication_NoLocalAuthorityScope_ReturnsBadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var result = await _sut.DeleteApplication(guid);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task DeleteApplication_NotFoundException_ReturnsNotFound()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockDeleteApplicationUseCase.Setup(x => x.Execute(guid, localAuthorityIds)).ThrowsAsync(new NotFoundException("Application not found"));

        // Act
        var result = await _sut.DeleteApplication(guid);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        var errorResponse = notFoundResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("Application not found");
    }

    [Test]
    public async Task DeleteApplication_UnauthorizedAccessException_ReturnsBadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockDeleteApplicationUseCase.Setup(x => x.Execute(guid, localAuthorityIds)).ThrowsAsync(new UnauthorizedAccessException("Unauthorized"));

        // Act
        var result = await _sut.DeleteApplication(guid);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("Unauthorized");
    }

    [Test]
    public async Task DeleteApplication_GeneralException_ReturnsBadRequest()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        _mockDeleteApplicationUseCase.Setup(x => x.Execute(guid, localAuthorityIds)).ThrowsAsync(new Exception("General error"));

        // Act
        var result = await _sut.DeleteApplication(guid);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse!.Errors!.First().Title.Should().Be("General error");
    }
}