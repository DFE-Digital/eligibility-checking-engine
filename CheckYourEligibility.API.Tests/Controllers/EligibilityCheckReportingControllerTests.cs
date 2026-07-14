using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework.Constraints;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests.Controllers;

public class EligibilityCheckReportingControllerTests : TestBase
{
    private IConfigurationRoot _configuration;
    private Mock<IAudit> _mockAuditGateway;

    private Mock<IGetEligibilityCheckReportingUseCase> _mockEligibilityCheckReportingUseCase;
    private Mock<IGetEligibilityReportStatusUseCase> _mockGetEligibilityReportStatusUseCase;
    private Mock<IGetEligibilityReportHistoryUseCase> _mockGetEligibilityReportHistoryUseCase;
    private Mock<IDeleteEligibilityCheckReportUseCase> _mockDeleteEligibilityCheckReportUseCase;
    private Mock<IGetEligibilityCheckReportItemsUseCase> _mockGetEligibilityCheckReportItemsUseCase;
    private ILogger<EligibilityCheckReportingController> _mockLogger;

    private EligibilityCheckReportingController _sut;

    [SetUp]
    public void SetUp()
    {
        _mockEligibilityCheckReportingUseCase = new Mock<IGetEligibilityCheckReportingUseCase>(MockBehavior.Strict);
        _mockGetEligibilityReportHistoryUseCase = new Mock<IGetEligibilityReportHistoryUseCase>(MockBehavior.Strict);
        _mockDeleteEligibilityCheckReportUseCase = new Mock<IDeleteEligibilityCheckReportUseCase>(MockBehavior.Strict);
        _mockGetEligibilityReportStatusUseCase = new Mock<IGetEligibilityReportStatusUseCase>(MockBehavior.Strict);
        _mockGetEligibilityCheckReportItemsUseCase = new Mock<IGetEligibilityCheckReportItemsUseCase>(MockBehavior.Strict);

        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<EligibilityCheckReportingController>>();

        var configData = new Dictionary<string, string?>
        {
            { "Jwt:Scopes:local_authority", "local_authority" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();


        _sut = new EligibilityCheckReportingController(
            _mockLogger,
            _configuration,
            _mockAuditGateway.Object,
            _mockGetEligibilityReportHistoryUseCase.Object,
            _mockEligibilityCheckReportingUseCase.Object,
            _mockDeleteEligibilityCheckReportUseCase.Object,
            _mockGetEligibilityReportStatusUseCase.Object,
            _mockGetEligibilityCheckReportItemsUseCase.Object
        );
    }

    [TearDown]
    public void Teardown()
    {
        _mockEligibilityCheckReportingUseCase.VerifyAll();
        _mockGetEligibilityReportHistoryUseCase.VerifyAll();
        _mockDeleteEligibilityCheckReportUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private void SetupControllerWithLocalAuthorityIds(List<int> localAuthorityIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>();
        claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "unit-test-bulk-check-controller"));
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
    public async Task EligibilityCheckReportItems_ReturnsOk_WhenUseCaseReturnsData()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });
        var reportId = "123e4567-e89b-12d3-a456-426614174000";
        var response = new EligibilityCheckReportItemsResponse { Data = new List<CheckItem> { new CheckItem { ParentName = "Smith", NationalInsuranceNumber = "AB123456C", DateOfBirth = "2000-01-01", CheckSubmittedDate = "2024-01-01", Outcome = "Success", Tier = "1", CheckType = "TypeA", CheckedBy = "admin" } } };
        _mockGetEligibilityCheckReportItemsUseCase.Setup(x => x.Execute(reportId)).ReturnsAsync(response);
        // Act
        var result = await _sut.EligibilityCheckReportItems(reportId);
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Test]
    public async Task EligibilityCheckReportItems_ReturnsBadRequest_WhenNoLocalAuthorityScope()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int>());
        // Act
        var result = await _sut.EligibilityCheckReportItems("any-id");
        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var errorResponse = badRequest.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors[0].Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task EligibilityCheckReportItems_ReturnsNotFound_WhenNotFoundExceptionThrown()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });
        _mockGetEligibilityCheckReportItemsUseCase.Setup(x => x.Execute(It.IsAny<string>())).ThrowsAsync(new NotFoundException());
        // Act
        var result = await _sut.EligibilityCheckReportItems("notfound-id");
        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task EligibilityCheckReportItems_ReturnsBadRequest_WhenValidationExceptionThrown()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });
        _mockGetEligibilityCheckReportItemsUseCase.Setup(x => x.Execute(It.IsAny<string>())).ThrowsAsync(new ValidationException(null, "Invalid report ID format. Must be a GUID"));
        // Act
        var result = await _sut.EligibilityCheckReportItems("bad-guid");
        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var errorResponse = badRequest.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors[0].Title.Should().Be("Invalid report ID format. Must be a GUID");
    }
    [Test]
    public async Task GetEligibilityCheckReport_returns_accepted_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        int localAuthorityId = 201;
        var reportRequest = new EligibilityCheckReportRequest { LocalAuthorityID = localAuthorityId };
        var reportResponse = new EligibilityCheckReportResponse
        {
            Data = new EligibilityCheckReportResponseItem
            {
                ReportID = Guid.NewGuid().ToString(),
                Status = ReportStatus.New.ToString()
            }
        };

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockEligibilityCheckReportingUseCase.Setup(u => u.Execute(It.Is<EligibilityCheckReportRequest>(r => r.LocalAuthorityID == localAuthorityId),It.IsAny<CheckMetaData>())).ReturnsAsync(reportResponse);

        // Act
        var response = await _sut.EligibilityCheckReportRequest(reportRequest);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;

        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        objectResult.Value.Should().BeEquivalentTo(reportResponse);
    }

    [Test]
    public async Task EligibilityCheckReportRequest_returns_bad_request_when_no_local_authority_scope_found()
    {
        // Arrange
        var model = new EligibilityCheckReportRequest();

        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.EligibilityCheckReportRequest(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)response;
        var errorResponse = badRequest.Value as ErrorResponse;

        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task EligibilityCheckReportRequest_returns_bad_request_when_fluent_validation_exception_thrown()
    {
        // Arrange
        var model = new EligibilityCheckReportRequest { LocalAuthorityID = 201 };

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockEligibilityCheckReportingUseCase
            .Setup(u => u.Execute(It.IsAny<EligibilityCheckReportRequest>(), It.IsAny<CheckMetaData>()))
            .ThrowsAsync(new FluentValidation.ValidationException("Validation failed"));

        // Act
        var response = await _sut.EligibilityCheckReportRequest(model);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)response;
        var errorResponse = badRequest.Value as ErrorResponse;

        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.First().Title.Should().Contain("Validation failed");
    }




    [Test]
    public async Task GetAllReportHistory_returns_bad_request_when_no_local_authority_scope_found()
    {
        // Arrange
        var localAuthorityId = "201";

        // Set up controller with NO local authority context (empty scopes)
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var response = await _sut.GetAllReportHistory(localAuthorityId, pageNumber: 1);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task GetAllReportHistory_returns_ok_with_report_history_response()
    {
        // Arrange
        var localAuthorityId = "201";
        var pageNumber = 2;

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var responseFromUseCase = new EligibilityCheckReportHistoryResponse
        {
            PageNumber = 2,
            PageSize = 10,
            TotalNumberOfRecords = 15,
            Data =
            [
                new EligibilityCheckReportHistoryItem
            {
                ReportID = Guid.NewGuid().ToString(),
                GeneratedBy = "peterB",
                NumberOfResults = 5,
                Status = "Complete"
            }
            ]
        };

        _mockGetEligibilityReportHistoryUseCase
            .Setup(u => u.Execute(localAuthorityId, It.IsAny<List<int>>(), pageNumber))
            .ReturnsAsync(responseFromUseCase);

        // Act
        var result = await _sut.GetAllReportHistory(localAuthorityId, pageNumber);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var okResult = (ObjectResult)result;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(responseFromUseCase);
    }

    [Test]
    public async Task GetAllReportHistory_returns_bad_request_for_fluent_validation_exception()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetEligibilityReportHistoryUseCase
            .Setup(u => u.Execute(It.IsAny<string>(), It.IsAny<List<int>>(), It.IsAny<int>()))
            .ThrowsAsync(new FluentValidation.ValidationException("Invalid page number"));

        // Act
        var result = await _sut.GetAllReportHistory("201", -1);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;
        var error = (ErrorResponse)badRequest.Value!;

        error.Errors.Single().Title.Should().Contain("Invalid page number");
    }

    [Test]
    public async Task DeleteReportHistory_returns_no_content_when_report_is_successfully_deleted()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteEligibilityCheckReportUseCase
            .Setup(u => u.Execute(reportId, It.IsAny<List<int>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteReportHistory(reportId);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        ((StatusCodeResult)result).StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Test]
    public async Task DeleteReportHistory_returns_forbid_when_unauthorized_access_exception_thrown()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteEligibilityCheckReportUseCase
            .Setup(u => u.Execute(reportId, It.IsAny<List<int>>()))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to delete reports for this local authority"));

        // Act
        var result = await _sut.DeleteReportHistory(reportId);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Test]
    public async Task DeleteReportHistory_returns_not_found_when_report_doesnt_exist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockDeleteEligibilityCheckReportUseCase
            .Setup(u => u.Execute(reportId, It.IsAny<List<int>>()))
            .ThrowsAsync(new NotFoundException("Eligibility report not found"));

        // Act
        var result = await _sut.DeleteReportHistory(reportId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        var errorResponse = (ErrorResponse)notFoundResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("Eligibility report not found");
    }

    [Test]
    public async Task DeleteReportHistory_returns_bad_request_when_no_local_authority_scope_found()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        SetupControllerWithLocalAuthorityIds(new List<int>()); // Empty scopes

        // Act
        var result = await _sut.DeleteReportHistory(reportId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("No local authority scope found");
    }
    [Test]
    public async Task EligibilityCheckReportStatus_returns_ok_with_status_response()
    {
        // Arrange
        var reportId = "report-123";
        var expectedResponse = new EligibilityReportStatusResponse { Status = "Complete" };
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });
        _mockGetEligibilityReportStatusUseCase
            .Setup(u => u.Execute(reportId))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.EligibilityCheckReportStatus(reportId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Test]
    public async Task EligibilityCheckReportStatus_returns_bad_request_when_no_local_authority_scope_found()
    {
        // Arrange
        var reportId = "report-123";
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var result = await _sut.EligibilityCheckReportStatus(reportId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var errorResponse = badRequest.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.First().Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task EligibilityCheckReportStatus_returns_not_found_when_not_found_exception_thrown()
    {
        // Arrange
        var reportId = "report-123";
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });
        _mockGetEligibilityReportStatusUseCase
            .Setup(u => u.Execute(reportId))
            .ThrowsAsync(new NotFoundException());

        // Act
        var result = await _sut.EligibilityCheckReportStatus(reportId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = (NotFoundObjectResult)result;
        var errorResponse = notFound.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.First().Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task EligibilityCheckReportStatus_returns_bad_request_when_validation_exception_thrown()
    {
        // Arrange
        var reportId = "report-123";
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });
        var errorMsg = "Validation failed";
        _mockGetEligibilityReportStatusUseCase
            .Setup(u => u.Execute(reportId))
            .ThrowsAsync(new ValidationException(new List<Error>(), errorMsg));

        // Act
        var result = await _sut.EligibilityCheckReportStatus(reportId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var errorResponse = badRequest.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.First().Title.Should().Be(errorMsg);
    }
}


