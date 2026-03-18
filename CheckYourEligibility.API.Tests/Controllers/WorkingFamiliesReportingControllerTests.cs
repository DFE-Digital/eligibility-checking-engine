using System.Security.Claims;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain;
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

public class WorkingFamiliesReportingControllerTests : TestBase.TestBase
{
    private IConfigurationRoot _configuration;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase> _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase;
    private ILogger<WorkingFamiliesReportingController> _mockLogger;
    private WorkingFamiliesReportingController _sut;

    [SetUp]
    public void Setup()
    {
        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase = new Mock<IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<WorkingFamiliesReportingController>>();

        var configData = new Dictionary<string, string?>
        {
            { "Jwt:Scopes:local_authority", "local_authority" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new WorkingFamiliesReportingController(
            _mockLogger,
           _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase.Object,
           _mockAuditGateway.Object,
           _configuration
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
        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase.VerifyAll();
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
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_ReturnsOk_WhenUseCaseReturnsData()
    {
        // Arrange
        var eligibilityCode = "TEST123";
        
        // Setup controller with local authority claims
        var localAuthorityIds = new List<int> { 1 };
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // single app
        var expectedResponse = new WorkingFamilyEventByEligibilityCodeRepsonse
        {
            Data = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
        {
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "X1",
                    EligibilityCode = eligibilityCode
                }
            }
        }
        };

        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
            .Setup(x => x.Execute(eligibilityCode, localAuthorityIds))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode)
            as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        result.Value.Should().Be(expectedResponse);
    }

    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_ReturnsOk_WhenMultipleBlocksReturned()
    {
        // Arrange
        var eligibilityCode = "TEST-MULTI-BLOCK";

        // Setup controller with local authority claims
        var localAuthorityIds = new List<int> { 1 };
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        var multiBlockResponse = new WorkingFamilyEventByEligibilityCodeRepsonse
        {
            Data = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
        {
            // BLOCK 3 (newest)
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "C3-A",
                    EligibilityCode = eligibilityCode,
                    SubmissionDate = new DateTime(2025,08,01)
                }
            },
            new()
            {
                Event = WorkingFamilyEventType.Reconfirm,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "C3-R1",
                    EligibilityCode = eligibilityCode,
                    SubmissionDate = new DateTime(2025,08,10)
                }
            },

            // BLOCK 2
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "C2-A",
                    EligibilityCode = eligibilityCode,
                    SubmissionDate = new DateTime(2024,12,15)
                }
            },
            new()
            {
                Event = WorkingFamilyEventType.Reconfirm,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "C2-R1",
                    EligibilityCode = eligibilityCode,
                    SubmissionDate = new DateTime(2024,12,20)
                }
            },

            // BLOCK 1 (oldest)
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "C1-A",
                    EligibilityCode = eligibilityCode,
                    SubmissionDate = new DateTime(2024,06,01)
                }
            }
        }
        };

        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
            .Setup(x => x.Execute(eligibilityCode, localAuthorityIds))
            .ReturnsAsync(multiBlockResponse);

        // Act
        var result = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode)
            as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var returned = result.Value as WorkingFamilyEventByEligibilityCodeRepsonse;
        returned.Should().NotBeNull();
        returned!.Data.Should().HaveCount(5);

        // Block 3 items appear first (newest submission dates)
        returned.Data[0].Record.WorkingFamiliesEventID.Should().Be("C3-A");
        returned.Data[1].Record.WorkingFamiliesEventID.Should().Be("C3-R1");

        // Block 2 next
        returned.Data[2].Record.WorkingFamiliesEventID.Should().Be("C2-A");
        returned.Data[3].Record.WorkingFamiliesEventID.Should().Be("C2-R1");

        // Block 1 last
        returned.Data[4].Record.WorkingFamiliesEventID.Should().Be("C1-A");

        // Use case called once
        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase.Verify(
            x => x.Execute(eligibilityCode, It.IsAny<List<int>>()),
            Times.Once);
    }

    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var eligibilityCode = "1234567";
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        // Setup controller with local authority claims
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
            .Setup(u => u.Execute(eligibilityCode, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value!).Errors.First().Title.Should().Be("Validation error");
    }


}
