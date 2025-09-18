using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Enums;
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
using System.Security.Claims;
using ValidationException = FluentValidation.ValidationException;

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
    private Mock<IDeleteBulkCheckUseCase> _mockDeleteBulkCheckUseCase;
    private Mock<IGetAllBulkChecksUseCase> _mockGetAllBulkChecksUseCase;

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
        _mockDeleteBulkCheckUseCase = new Mock<IDeleteBulkCheckUseCase>(MockBehavior.Strict);
        _mockGetAllBulkChecksUseCase = new Mock<IGetAllBulkChecksUseCase>(MockBehavior.Strict);
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
            _mockGetEligibilityCheckItemUseCase.Object,
            _mockDeleteBulkCheckUseCase.Object,
            _mockGetAllBulkChecksUseCase.Object
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
        var request = _fixture.Create<CheckEligibilityRequest<CheckEligibilityRequestData>>();
        var executionResult = new CheckEligibilityResponse();

        _mockCheckEligibilityUseCase.Setup(u => u.Execute(request, CheckEligibilityType.FreeSchoolMeals))
            .ThrowsAsync(new ValidationException("Validation error"));

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
        var request = _fixture.Create<CheckEligibilityRequest<CheckEligibilityRequestData>>();
        var statusResponse = _fixture.Create<CheckEligibilityResponse>();
        var executionResult = statusResponse;

        _mockCheckEligibilityUseCase.Setup(u => u.Execute(request, CheckEligibilityType.FreeSchoolMeals))
            .ReturnsAsync(executionResult);

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
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, CheckEligibilityType.FreeSchoolMeals,
                _configuration.GetValue<int>("BulkEligibilityCheckLimit")))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.CheckEligibilityBulkFsm(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value!).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task CheckEligibilityBulk_returns_accepted_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestBulk>();
        var bulkResponse = _fixture.Create<CheckEligibilityResponseBulk>();
        var executionResult = bulkResponse;
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, CheckEligibilityType.FreeSchoolMeals,
                _configuration.GetValue<int>("BulkEligibilityCheckLimit")))
            .ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityBulkFsm(request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        objectResult.Value.Should().Be(bulkResponse);
    }

    #region Working Families

    /// <summary>
    /// In this test we ensure 
    /// 1.The Code correctly catches the exception.
    /// 2.Translates it into a BadRequestObjectResult.
    /// 3.Passes the exception message into the error response.
    /// </summary>
    [Test]
    public async Task CheckEligibility_WF_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>>();
        _mockCheckEligibilityUseCase.Setup(u => u.Execute(request, CheckEligibilityType.WorkingFamilies))
            .ThrowsAsync(new ValidationException("Validation error"));

        var response = await _sut.CheckEligibilityWF(request);
        response.Should().BeOfType<BadRequestObjectResult>();
        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    /// <summary>
    /// In this test we ensure 
    /// 1. Correct response code is returned on success
    /// </summary>
    [Test]
    public async Task CheckEligibility_WF_returns_accepted_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>>();
        var statusResponse = _fixture.Create<CheckEligibilityResponse>();
        var executionResult = statusResponse;

        _mockCheckEligibilityUseCase.Setup(u => u.Execute(request, CheckEligibilityType.WorkingFamilies))
            .ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityWF(request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        objectResult.Value.Should().Be(statusResponse);
    }

    [Test]
    public async Task CheckEligibilityBulk_WF_returns_accepted_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestWorkingFamiliesBulk>();
        var bulkResponse = _fixture.Create<CheckEligibilityResponseBulk>();
        var executionResult = bulkResponse;
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Set up HttpContext for bulk check path
        var httpContext = new DefaultHttpContext();
        var path = new PathString("/bulk-check/working-families");
        httpContext.Request.Path = path;
        
        // Preserve the existing user context and add the path
        if (_sut.ControllerContext.HttpContext.User != null)
        {
            httpContext.User = _sut.ControllerContext.HttpContext.User;
        }
        
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, CheckEligibilityType.WorkingFamilies,
                _configuration.GetValue<int>("BulkEligibilityCheckLimit")))
            .ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityBulkWF(request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        objectResult.Value.Should().Be(bulkResponse);
    }

    [Test]
    public async Task CheckEligibilityBulk_WF_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestWorkingFamiliesBulk>();
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Set up HttpContext for bulk check path
        var httpContext = new DefaultHttpContext();
        var path = new PathString("/bulk-check/working-families");
        httpContext.Request.Path = path;
        
        // Preserve the existing user context and add the path
        if (_sut.ControllerContext.HttpContext.User != null)
        {
            httpContext.User = _sut.ControllerContext.HttpContext.User;
        }
        
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, CheckEligibilityType.WorkingFamilies,
                _configuration.GetValue<int>("BulkEligibilityCheckLimit")))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.CheckEligibilityBulkWF(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value!).Errors.First().Title.Should().Be("Validation error");
    }

    #endregion

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
            .ThrowsAsync(new ValidationException("Validation error"));

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
            .ThrowsAsync(new ValidationException("Validation error"));

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

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, CheckEligibilityType.None))
            .ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.CheckEligibilityStatus(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task CheckEligibilityStatusByType_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, type)).ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.CheckEligibilityStatus(type, guid);

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

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, CheckEligibilityType.None))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.CheckEligibilityStatus(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task CheckEligibilityStatusByType_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var executionResult = new CheckEligibilityStatusResponse();

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, type))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.CheckEligibilityStatus(type, guid);

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

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, CheckEligibilityType.None))
            .ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityStatus(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(statusResponse);
    }

    [Test]
    public async Task CheckEligibilityStatusByType_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var statusResponse = _fixture.Create<CheckEligibilityStatusResponse>();
        var executionResult = statusResponse;

        _mockGetEligibilityCheckStatusUseCase.Setup(u => u.Execute(guid, type)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.CheckEligibilityStatus(type, guid);

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

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid, CheckEligibilityType.None))
            .ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.EligibilityCheck(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be(guid);
    }

    [Test]
    public async Task EligibilityCheckByType_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var executionResult = new CheckEligibilityItemResponse();

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid, type)).ThrowsAsync(new NotFoundException());

        // Act
        var response = await _sut.EligibilityCheck(type, guid);

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

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid, CheckEligibilityType.None))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.EligibilityCheck(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test]
    public async Task EligibilityCheckByType_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var executionResult = new CheckEligibilityItemResponse();

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid, type))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.EligibilityCheck(type, guid);

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

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid, CheckEligibilityType.None))
            .ReturnsAsync(executionResult);

        // Act
        var response = await _sut.EligibilityCheck(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(itemResponse);
    }

    [Test]
    public async Task EligibilityCheckByType_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var itemResponse = _fixture.Create<CheckEligibilityItemResponse>();
        var executionResult = itemResponse;

        _mockGetEligibilityCheckItemUseCase.Setup(u => u.Execute(guid, type)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.EligibilityCheck(type, guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(itemResponse);
    }
}