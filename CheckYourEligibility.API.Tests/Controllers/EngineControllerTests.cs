using System.Security.Claims;
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ValidationException = FluentValidation.ValidationException;

namespace CheckYourEligibility.API.Tests;

public class EngineControllerTests : TestBase.TestBase
{
    private IConfigurationRoot _configuration;
    private Mock<IAudit> _mockAuditGateway;

    private Mock<ICheckEligibilityBulkUseCase> _mockCheckEligibilityBulkUseCase;
    private Mock<ICheckEligibilityUseCase> _mockCheckEligibilityUseCase;

    private Mock<IGetBulkCheckStatusesUseCase> _mockGetBulkCheckStatusesUseCase;
    private Mock<IGetBulkUploadProgressUseCase> _mockGetBulkUploadProgressUseCase;
    private Mock<IGetBulkUploadResultsUseCase> _mockGetBulkUploadResultsUseCase;
    private Mock<IGetEligibilityCheckItemUseCase> _mockGetEligibilityCheckItemUseCase;
    private Mock<IGetEligibilityCheckStatusUseCase> _mockGetEligibilityCheckStatusUseCase;
    private ILogger<EngineController> _mockLogger;
    private Mock<IProcessEligibilityCheckUseCase> _mockProcessEligibilityCheckUseCase;
    private Mock<IProcessEligibilityBulkCheckUseCase> _mockProcessEligibilityBulkCheckUseCase;
    private Mock<IUpdateEligibilityCheckStatusUseCase> _mockUpdateEligibilityCheckStatusUseCase;
    private Mock<IDeleteBulkCheckUseCase> _mockDeleteBulkCheckUseCase;
    private Mock<IGetAllBulkChecksUseCase> _mockGetAllBulkChecksUseCase;

    private EngineController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCheckEligibilityBulkUseCase = new Mock<ICheckEligibilityBulkUseCase>(MockBehavior.Strict);
        _mockCheckEligibilityUseCase = new Mock<ICheckEligibilityUseCase>(MockBehavior.Strict);
        _mockProcessEligibilityBulkCheckUseCase = new Mock<IProcessEligibilityBulkCheckUseCase>(MockBehavior.Strict);
        _mockGetBulkCheckStatusesUseCase = new Mock<IGetBulkCheckStatusesUseCase>(MockBehavior.Strict);
        _mockGetBulkUploadProgressUseCase = new Mock<IGetBulkUploadProgressUseCase>(MockBehavior.Strict);
        _mockGetBulkUploadResultsUseCase = new Mock<IGetBulkUploadResultsUseCase>(MockBehavior.Strict);
        _mockGetEligibilityCheckStatusUseCase = new Mock<IGetEligibilityCheckStatusUseCase>(MockBehavior.Strict);
        _mockUpdateEligibilityCheckStatusUseCase = new Mock<IUpdateEligibilityCheckStatusUseCase>(MockBehavior.Strict);
        _mockProcessEligibilityCheckUseCase = new Mock<IProcessEligibilityCheckUseCase>(MockBehavior.Strict);
        _mockGetEligibilityCheckItemUseCase = new Mock<IGetEligibilityCheckItemUseCase>(MockBehavior.Strict);
        _mockDeleteBulkCheckUseCase = new Mock<IDeleteBulkCheckUseCase>(MockBehavior.Strict);
        _mockGetAllBulkChecksUseCase = new Mock<IGetAllBulkChecksUseCase>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<EngineController>>();

        var configForBulkUpload = new Dictionary<string, string>
        {
            { "BulkEligibilityCheckLimit", "5" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForBulkUpload)
            .Build();

        _sut = new EngineController(
            _mockLogger,
            _mockAuditGateway.Object,
            _mockProcessEligibilityBulkCheckUseCase.Object,
            _mockUpdateEligibilityCheckStatusUseCase.Object,
            _mockProcessEligibilityCheckUseCase.Object
        );

        // Setup default HttpContext with a Mock HttpRequest
        var httpContext = new DefaultHttpContext();
        var request = new Mock<HttpRequest>();
        var path = new PathString("/check/free-school-meals");
        request.Setup(r => r.Path).Returns(path);
        httpContext.Request.Path = path;
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [TearDown]
    public void Teardown()
    {
        _mockProcessEligibilityBulkCheckUseCase.VerifyAll();
        _mockCheckEligibilityUseCase.VerifyAll();
        _mockCheckEligibilityBulkUseCase.VerifyAll();
        _mockGetBulkCheckStatusesUseCase.VerifyAll();
        _mockGetBulkUploadProgressUseCase.VerifyAll();
        _mockGetBulkUploadResultsUseCase.VerifyAll();
        _mockGetEligibilityCheckStatusUseCase.VerifyAll();
        _mockUpdateEligibilityCheckStatusUseCase.VerifyAll();
        _mockProcessEligibilityCheckUseCase.VerifyAll();
        _mockGetEligibilityCheckItemUseCase.VerifyAll();
        _mockDeleteBulkCheckUseCase.VerifyAll();
        _mockGetAllBulkChecksUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
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
    public async Task ProcessQueue_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var queue = _fixture.Create<string>();
        var executionResult = new MessageResponse
        {
            Data = "Invalid Request."
        };

        _mockProcessEligibilityBulkCheckUseCase.Setup(u => u.Execute(queue)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.ProcessQueue(queue);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task ProcessQueue_returns_ok_when_use_case_returns_valid_result()
    {
        // Arrange
        var queue = _fixture.Create<string>();
        var messageResponse = _fixture.Create<MessageResponse>();
        _mockProcessEligibilityBulkCheckUseCase.Setup(u => u.Execute(queue)).ReturnsAsync(messageResponse);

        // Act
        var response = await _sut.ProcessQueue(queue);

        // Assert
        response.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)response;
        okResult.Value.Should().Be(messageResponse);
    }

    [Test]
    public async Task EligibilityCheckStatusUpdate_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockUpdateEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, request))
            .ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.EligibilityCheckStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task EligibilityCheckStatusUpdate_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockUpdateEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, request))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.EligibilityCheckStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task EligibilityCheckStatusUpdate_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        var executionResult = statusResponse;

        _mockUpdateEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, request)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.EligibilityCheckStatusUpdate(guid, request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(statusResponse);
    }

    [Test]
    public async Task Process_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid, null)).ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        notFoundResult.Value.Equals(new ErrorResponse { Errors = [new Error { Title = guid }] });
    }

    [Test]
    public async Task Process_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid, null))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task Process_returns_gateway_unavailable_when_use_case_returns_gateway_unavailable()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        var executionResult = statusResponse;

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid,null))
            .ThrowsAsync(new ApplicationException("Service unavailable"));

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
    [Test]
    public async Task Process_returns_503_when_use_case_returns_status_queue_for_processing()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        statusResponse.Data.Status = "queuedForProcessing";
        var executionResult = statusResponse;

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid, null)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        ((ErrorResponse)objectResult.Value).Errors.First().Title.Should().Be("Eligibility check still queued for processing");
    }

    [Test]
    public async Task Process_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        statusResponse.Data.Status = "eligible";
        var executionResult = statusResponse;

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid,null)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(statusResponse);
    }

    [Test]
    public async Task Process_returns_bad_request_when_ProcessCheckException_is_thrown()
    {
        // Arrange
        var guid = _fixture.Create<string>();

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid,null)).ThrowsAsync(new ProcessCheckException());

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be(guid);
    }
}