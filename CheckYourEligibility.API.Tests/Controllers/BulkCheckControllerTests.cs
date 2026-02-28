using System.Security.Claims;
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
using ValidationException = FluentValidation.ValidationException;

namespace CheckYourEligibility.API.Tests;

public class BulkCheckControllerTests : TestBase.TestBase
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
    private ILogger<BulkCheckController> _mockLogger;
    private Mock<IProcessEligibilityCheckUseCase> _mockProcessEligibilityCheckUseCase;
    private Mock<IProcessEligibilityBulkCheckUseCase> _mockProcessEligibilityBulkCheckUseCase;
    private Mock<IUpdateEligibilityCheckStatusUseCase> _mockUpdateEligibilityCheckStatusUseCase;
    private Mock<IDeleteBulkCheckUseCase> _mockDeleteBulkCheckUseCase;
    private Mock<IGetAllBulkChecksUseCase> _mockGetAllBulkChecksUseCase;
    private Mock<IGenerateEligibilityCheckReportUseCase> _mockGenerateEligibilityCheckReportUseCase;

    private BulkCheckController _sut;

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
        _mockGenerateEligibilityCheckReportUseCase = new Mock<IGenerateEligibilityCheckReportUseCase>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<BulkCheckController>>();

        var configForBulkUpload = new Dictionary<string, string>
        {
            { "BulkEligibilityCheckLimit", "5" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForBulkUpload)
            .Build();

        _sut = new BulkCheckController(
            _mockLogger,
            _mockAuditGateway.Object,
            _configuration,
            _mockCheckEligibilityBulkUseCase.Object,
            _mockGetBulkCheckStatusesUseCase.Object,
            _mockGetBulkUploadProgressUseCase.Object,
            _mockGetBulkUploadResultsUseCase.Object,
            _mockDeleteBulkCheckUseCase.Object,
            _mockGenerateEligibilityCheckReportUseCase.Object,
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
        _mockGenerateEligibilityCheckReportUseCase.VerifyAll();
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
    public async Task CheckEligibilityBulk_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestBulk>();
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1
        var meta= _fixture.Create<CheckMetaData>();

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, CheckEligibilityType.FreeSchoolMeals,
                _configuration.GetValue<int>("BulkEligibilityCheckLimit"), meta))
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
        var meta = _fixture.Create<CheckMetaData>();
        var bulkResponse = _fixture.Create<CheckEligibilityResponseBulk>();
        var executionResult = bulkResponse;
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockCheckEligibilityBulkUseCase
            .Setup(u => u.Execute(request, CheckEligibilityType.FreeSchoolMeals,
                _configuration.GetValue<int>("BulkEligibilityCheckLimit"), meta))
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
        var meta = _fixture.Create<CheckMetaData>();
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
                _configuration.GetValue<int>("BulkEligibilityCheckLimit"),meta))
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
        var meta = _fixture.Create<CheckMetaData>();
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
                _configuration.GetValue<int>("BulkEligibilityCheckLimit"),meta))
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
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201)))).ThrowsAsync(new NotFoundException(guid));

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
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201))))
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
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201)))).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.BulkUploadResults(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(bulkResponse);
    }

    [Test]
    public async Task BulkUploadResults_returns_unauthorized_when_use_case_throws_UnauthorizedAccessException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetBulkUploadResultsUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201))))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to access bulk check"));

        // Act
        var response = await _sut.BulkUploadResults(guid);

        // Assert
        response.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = (UnauthorizedObjectResult)response;
        var errorResponse = (ErrorResponse)unauthorizedResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("You do not have permission to access bulk check");
    }

    [Test]
    public async Task BulkUploadResults_returns_bad_request_when_no_local_authority_scope()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        
        // Set up controller with NO local authority context (empty scopes)
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.BulkUploadResults(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task DeleteBulkUpload_returns_not_found_when_use_case_returns_not_found()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteBulkCheckUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201)))).ThrowsAsync(new NotFoundException($"Bulk check with ID {guid} not found."));

        // Act
        var response = await _sut.DeleteBulkUpload(guid);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)response;
        ((ErrorResponse)notFoundResult.Value).Errors.First().Title.Should().Be($"Bulk check with ID {guid} not found.");
    }

    [Test]
    public async Task DeleteBulkUpload_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var guid = "";
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteBulkCheckUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201))))
            .ThrowsAsync(new ValidationException("Invalid Request, group ID is required."));

        // Act
        var response = await _sut.DeleteBulkUpload(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Invalid Request, group ID is required.");
    }

    [Test]
    public async Task DeleteBulkUpload_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var executionResult = _fixture.Create<CheckEligibilityBulkDeleteResponse>();
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteBulkCheckUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201)))).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.DeleteBulkUpload(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().Be(executionResult);
    }

    [Test]
    public async Task DeleteBulkUpload_returns_forbidden_when_use_case_throws_UnauthorizedAccessException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        
        // Set up controller with local authority context
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteBulkCheckUseCase.Setup(u => u.Execute(guid, It.Is<IList<int>>(ids => ids.Contains(201))))
            .ThrowsAsync(new InvalidScopeException("Access denied. You can only delete bulk checks for your assigned local authority."));

        // Act
        var response = await _sut.DeleteBulkUpload(guid);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var forbiddenResult = (ObjectResult)response;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var errorResponse = (ErrorResponse)forbiddenResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("Access denied. You can only delete bulk checks for your assigned local authority.");
    }

    [Test]
    public async Task DeleteBulkUpload_returns_bad_request_when_no_local_authority_scope()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        
        // Set up controller with NO local authority context (empty scopes)
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.DeleteBulkUpload(guid);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task EligibilityCheckReportRequest_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var request = _fixture.Create<EligibilityCheckReportRequest>();
        var reportItems = _fixture.CreateMany<EligibilityCheckReportItem>(3).ToList();
        var executionResult = new EligibilityCheckReportResponse { Data = reportItems };

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGenerateEligibilityCheckReportUseCase.Setup(u => u.Execute(request)).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.EligibilityCheckReportRequest(request);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;

        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().BeEquivalentTo(executionResult);
    }

    [Test]
    public async Task EligibilityCheckReportRequest_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var request = _fixture.Create<EligibilityCheckReportRequest>();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGenerateEligibilityCheckReportUseCase.Setup(u => u.Execute(request))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.EligibilityCheckReportRequest(request);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value).Errors.First().Title.Should().Be("Validation error");
    }

    [Test] 
    public async Task EligibilityCheckReportRequest_returns_unauthorized_when_use_case_throws_UnauthorizedAccessException()
    {
        // Arrange
        var request = _fixture.Create<EligibilityCheckReportRequest>();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGenerateEligibilityCheckReportUseCase.Setup(u => u.Execute(request))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var response = await _sut.EligibilityCheckReportRequest(request);

        // Assert
        response.Should().BeOfType<UnauthorizedObjectResult>();
    }
}