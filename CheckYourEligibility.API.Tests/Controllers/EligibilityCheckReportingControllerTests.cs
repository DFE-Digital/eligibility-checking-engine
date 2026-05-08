
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.Controllers;

public class EligibilityCheckReportingControllerTests : TestBase.TestBase
{
    private IConfigurationRoot _configuration;
    private Mock<IAudit> _mockAuditGateway;

    private Mock<IGetEligibilityCheckReportingUseCase> _mockEligibilityCheckReportingUseCase;
    private Mock<IGetEligibilityReportHistoryUseCase> _mockGetEligibilityReportHistoryUseCase;
    private ILogger<EligibilityCheckReportingController> _mockLogger;

    private EligibilityCheckReportingController _sut;

    [SetUp]
    public void SetUp()
    {
        _mockEligibilityCheckReportingUseCase = new Mock<IGetEligibilityCheckReportingUseCase>(MockBehavior.Strict);
        _mockGetEligibilityReportHistoryUseCase = new Mock<IGetEligibilityReportHistoryUseCase>(MockBehavior.Strict);

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
            _mockEligibilityCheckReportingUseCase.Object
        );
    }

    [TearDown]
    public void Teardown()
    {
        _mockEligibilityCheckReportingUseCase.VerifyAll();
        _mockGetEligibilityReportHistoryUseCase.VerifyAll();
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
    public async Task GetEligibilityCheckReport_returns_ok_with_response_when_use_case_returns_valid_result()
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

        _mockEligibilityCheckReportingUseCase.Setup(u => u.Execute(It.Is<EligibilityCheckReportRequest>(r => r.LocalAuthorityID == localAuthorityId))).ReturnsAsync(reportResponse);

        // Act
        var response = await _sut.EligibilityCheckReportRequest(reportRequest);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;

        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().BeEquivalentTo(reportResponse);
    }

    [Test]
    public async Task GetEligibilityReportHistory_returns_ok_with_response_when_use_case_returns_valid_result()
    {
        // Arrange
        var localAuthorityId = "201";
        var reportHistory = _fixture.CreateMany<EligibilityCheckReportHistoryItem>(3).ToList();
        var executionResult = new EligibilityCheckReportHistoryResponse { Data = reportHistory };

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetEligibilityReportHistoryUseCase.Setup(u => u.Execute(localAuthorityId, It.Is<IList<int>>(ids => ids.Contains(201)))).ReturnsAsync(executionResult);

        // Act
        var response = await _sut.GetAllReportHistory(localAuthorityId);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)response;

        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().BeEquivalentTo(executionResult);
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
            .Setup(u => u.Execute(It.IsAny<EligibilityCheckReportRequest>()))
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
        var response = await _sut.GetAllReportHistory(localAuthorityId);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        errorResponse.Errors.First().Title.Should().Be("No local authority scope found");
    }
}


