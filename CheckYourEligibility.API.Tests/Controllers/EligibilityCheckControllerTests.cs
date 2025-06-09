using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class EligibilityCheckControllerTests : TestBase.TestBase
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
    private ILogger<EligibilityCheckController> _mockLogger;
    private Mock<IProcessEligibilityCheckUseCase> _mockProcessEligibilityCheckUseCase;
    private Mock<IProcessQueueMessagesUseCase> _mockProcessQueueMessagesUseCase;
    private Mock<IUpdateEligibilityCheckStatusUseCase> _mockUpdateEligibilityCheckStatusUseCase;
    private EligibilityCheckController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCheckEligibilityBulkUseCase = new Mock<ICheckEligibilityBulkUseCase>(MockBehavior.Strict);
        _mockCheckEligibilityUseCase = new Mock<ICheckEligibilityUseCase>(MockBehavior.Strict);
        _mockProcessQueueMessagesUseCase = new Mock<IProcessQueueMessagesUseCase>(MockBehavior.Strict);
        _mockGetBulkCheckStatusesUseCase = new Mock<IGetBulkCheckStatusesUseCase>(MockBehavior.Strict);
        _mockGetBulkUploadProgressUseCase = new Mock<IGetBulkUploadProgressUseCase>(MockBehavior.Strict);
        _mockGetBulkUploadResultsUseCase = new Mock<IGetBulkUploadResultsUseCase>(MockBehavior.Strict);
        _mockGetEligibilityCheckStatusUseCase = new Mock<IGetEligibilityCheckStatusUseCase>(MockBehavior.Strict);
        _mockUpdateEligibilityCheckStatusUseCase = new Mock<IUpdateEligibilityCheckStatusUseCase>(MockBehavior.Strict);
        _mockProcessEligibilityCheckUseCase = new Mock<IProcessEligibilityCheckUseCase>(MockBehavior.Strict);
        _mockGetEligibilityCheckItemUseCase = new Mock<IGetEligibilityCheckItemUseCase>(MockBehavior.Strict);

        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<EligibilityCheckController>>();

        var configForBulkUpload = new Dictionary<string, string>
        {
            { "BulkEligibilityCheckLimit", "5" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForBulkUpload)
            .Build();

        _sut = new EligibilityCheckController(
            _mockLogger,
            _mockAuditGateway.Object,
            _configuration,
            _mockProcessQueueMessagesUseCase.Object,
            _mockCheckEligibilityUseCase.Object,
            _mockCheckEligibilityBulkUseCase.Object,
            _mockGetBulkCheckStatusesUseCase.Object,
            _mockGetBulkUploadProgressUseCase.Object,
            _mockGetBulkUploadResultsUseCase.Object,
            _mockGetEligibilityCheckStatusUseCase.Object,
            _mockUpdateEligibilityCheckStatusUseCase.Object,
            _mockProcessEligibilityCheckUseCase.Object,
            _mockGetEligibilityCheckItemUseCase.Object
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
        _mockProcessQueueMessagesUseCase.VerifyAll();
        _mockCheckEligibilityUseCase.VerifyAll();
        _mockCheckEligibilityBulkUseCase.VerifyAll();
        _mockGetBulkUploadProgressUseCase.VerifyAll();
        _mockGetBulkUploadResultsUseCase.VerifyAll();
        _mockGetEligibilityCheckStatusUseCase.VerifyAll();
        _mockUpdateEligibilityCheckStatusUseCase.VerifyAll();
        _mockProcessEligibilityCheckUseCase.VerifyAll();
        _mockGetEligibilityCheckItemUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
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

        _mockProcessQueueMessagesUseCase.Setup(u => u.Execute(queue)).ReturnsAsync(executionResult);

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
        _mockProcessQueueMessagesUseCase.Setup(u => u.Execute(queue)).ReturnsAsync(messageResponse);

        // Act
        var response = await _sut.ProcessQueue(queue);

        // Assert
        response.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)response;
        okResult.Value.Should().Be(messageResponse);
    }

    [Test]
    public async Task CheckEligibility_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequest>();
        var executionResult = new CheckEligibilityResponse();

        _mockCheckEligibilityUseCase.Setup(u => u.Execute(request))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

        // Act
        var response = await _sut.CheckEligibilityFsm(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task CheckEligibility_returns_accepted_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequest>();
        var statusResponse = _fixture.Create<CheckEligibilityResponse>();
        var executionResult = statusResponse;

        _mockCheckEligibilityUseCase.Setup(u => u.Execute(request)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityFsm(request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        objectResult.Value.Should().Be(statusResponse);
    }

    [Test]
    public async Task CheckEligibilityBulk_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestBulk>();
        var executionResult = new CheckEligibilityResponseBulk();

        // Set up HttpContext for bulk check path
        var httpContext = new DefaultHttpContext();
        var path = new PathString("/bulk-check/free-school-meals");
        httpContext.Request.Path = path;
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, _configuration.GetValue<int>("BulkEligibilityCheckLimit")))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

        // Act
        var response = await _sut.CheckEligibilityBulkFsm(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task CheckEligibilityBulk_returns_accepted_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestBulk>();
        var bulkResponse = _fixture.Create<CheckEligibilityResponseBulk>();
        var executionResult = bulkResponse;

        // Set up HttpContext for bulk check path
        var httpContext = new DefaultHttpContext();
        var path = new PathString("/bulk-check/free-school-meals");
        httpContext.Request.Path = path;
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, _configuration.GetValue<int>("BulkEligibilityCheckLimit")))
            .ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityBulkFsm(request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        objectResult.Value.Should().Be(bulkResponse);
    }

    [Test]
    public async Task BulkUploadProgress_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityBulkStatusResponse();

        _mockGetBulkUploadProgressUseCase.Setup(u => u.Execute(guid)).ThrowsAsync(new NotFoundException(guid));

        // Act
        var response = await _sut.BulkUploadProgress(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task BulkUploadProgress_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityBulkStatusResponse();

        _mockGetBulkUploadProgressUseCase.Setup(u => u.Execute(guid))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

        // Act
        var response = await _sut.BulkUploadProgress(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task BulkUploadProgress_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusResponse = _fixture.Create<CheckEligibilityBulkStatusResponse>();
        var executionResult = statusResponse;

        _mockGetBulkUploadProgressUseCase.Setup(u => u.Execute(guid)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.BulkUploadProgress(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(statusResponse);
    }

    [Test]
    public async Task BulkUploadResults_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityBulkResponse();

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid)).ThrowsAsync(new NotFoundException(guid));

        // Act
        var response = await _sut.BulkUploadResults(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task BulkUploadResults_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityBulkResponse();

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

        // Act
        var response = await _sut.BulkUploadResults(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task BulkUploadResults_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var bulkResponse = _fixture.Create<CheckEligibilityBulkResponse>();
        var executionResult = bulkResponse;

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.BulkUploadResults(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(bulkResponse);
    }

    [Test]
    public async Task CheckEligibilityStatus_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid)).ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.CheckEligibilityStatus(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task CheckEligibilityStatus_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

        // Act
        var response = await _sut.CheckEligibilityStatus(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task CheckEligibilityStatus_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        var executionResult = statusResponse;

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityStatus(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(statusResponse);
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
            .ThrowsAsync(new ValidationException(null, "Validation error"));

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

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid)).ThrowsAsync(new NotFoundException());

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

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

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

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid))
            .ThrowsAsync(new ApplicationException("Service unavailable"));

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Test]
    public async Task Process_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        var executionResult = statusResponse;

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid)).ReturnsAsync(executionResult);

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

        _mockProcessEligibilityCheckUseCase.Setup(u => u.Execute(guid)).ThrowsAsync(new ProcessCheckException());

        // Act
        var response = await _sut.Process(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task EligibilityCheck_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityItemResponse();

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid)).ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.EligibilityCheck(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task EligibilityCheck_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = new CheckEligibilityItemResponse();

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid))
            .ThrowsAsync(new ValidationException(null, "Validation error"));

        // Act
        var response = await _sut.EligibilityCheck(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task EligibilityCheck_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var itemResponse = _fixture.Create<CheckEligibilityItemResponse>();
        var executionResult = itemResponse;

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.EligibilityCheck(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(itemResponse);
    }
}