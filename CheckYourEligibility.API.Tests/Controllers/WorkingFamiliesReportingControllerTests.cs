using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ValidationException = FluentValidation.ValidationException;

namespace CheckYourEligibility.API.Tests.Controllers;

public class WorkingFamiliesReportingControllerTests : TestBase.TestBase
{
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
        _sut = new WorkingFamiliesReportingController(
            _mockLogger,
           _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase.Object,
           _mockAuditGateway.Object
        );
    }

    [TearDown]
    public void Teardown()
    {
        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }


    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_ReturnsOk_WhenUseCaseReturnsData()
    {
        // Arrange
        var eligibilityCode = "TEST123";

        // single app
        var expectedResponse = new WorkingFamilyEventByEligibilityCodeRepsonse
        {
            Data = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
        {
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEventEligibilityCodeRepsonseRecord
                {
                    EventId = "X1",
                }
            }
        }
        };

        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
            .Setup(x => x.Execute(eligibilityCode))
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

        var multiBlockResponse = new WorkingFamilyEventByEligibilityCodeRepsonse
        {
            Data = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
        {
            // BLOCK 3 (newest)
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEventEligibilityCodeRepsonseRecord
                {
                    EventId = "C3-A",
                    SubmissionDate = new DateTime(2025,08,01)
                }
            },
            new()
            {
                Event = WorkingFamilyEventType.Reconfirm,
                Record = new WorkingFamiliesEventEligibilityCodeRepsonseRecord
                {
                    EventId = "C3-R1",
                    SubmissionDate = new DateTime(2025,08,10)
                }
            },

            // BLOCK 2
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEventEligibilityCodeRepsonseRecord
                {
                    EventId = "C2-A",
                    SubmissionDate = new DateTime(2024,12,15)
                }
            },
            new()
            {
                Event = WorkingFamilyEventType.Reconfirm,
                Record = new WorkingFamiliesEventEligibilityCodeRepsonseRecord
                {
                    EventId = "C2-R1",
                    SubmissionDate = new DateTime(2024,12,20)
                }
            },

            // BLOCK 1 (oldest)
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEventEligibilityCodeRepsonseRecord
                {
                    EventId = "C1-A",
                    SubmissionDate = new DateTime(2024,06,01)
                }
            }
        }
        };

        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
            .Setup(x => x.Execute(eligibilityCode))
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
        returned.Data[0].Record.EventId.Should().Be("C3-A");
        returned.Data[1].Record.EventId.Should().Be("C3-R1");

        // Block 2 next
        returned.Data[2].Record.EventId.Should().Be("C2-A");
        returned.Data[3].Record.EventId.Should().Be("C2-R1");

        // Block 1 last
        returned.Data[4].Record.EventId.Should().Be("C1-A");

        // Use case called once
        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase.Verify(
            x => x.Execute(eligibilityCode),
            Times.Once);
    }

    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_returns_bad_request_when_use_case_returns_invalid_result()
    {
        // Arrange
        var eligibilityCode = "1234567";
        var localAuthorityIds = new List<int> { 1 }; // Regular user with LA ID 1

        _mockGetAllWorkingFamiliesEventsByEligibilityCodeUseCase
            .Setup(u => u.Execute(eligibilityCode))
            .ThrowsAsync(new ValidationException("Validation error"));

        // Act
        var response = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)response;
        ((ErrorResponse)badRequestResult.Value!).Errors.First().Title.Should().Be("Validation error");
    }

}
