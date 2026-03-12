using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests;

[TestFixture]
public class EligibilityEventsControllerTests : TestBase.TestBase
{
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<IUpsertWorkingFamiliesEventUseCase> _mockUpsertUseCase = null!;
    private Mock<IDeleteWorkingFamiliesEventUseCase> _mockDeleteUseCase = null!;
    private ILogger<EligibilityEventsController> _mockLogger = null!;
    private EligibilityEventsController _sut = null!;

    private const string ValidHmrcId = "hmrc-event-guid-001";

    private EligibilityEventRequest ValidRequest => new EligibilityEventRequest
    {
        EligibilityEvent = new EligibilityEventData
        {
            Dern = "50009000005",
            SubmissionDate = new DateTime(2026, 1, 20),
            ValidityStartDate = new DateTime(2026, 1, 21),
            ValidityEndDate = new DateTime(2026, 4, 23),
            Parent = new ParentPartnerData { Nino = "AA123456A", Forename = "John", Surname = "Smith" },
            Child = new ChildData { Forename = "Charles", Surname = "Smith", Dob = new DateTime(2012, 4, 23), PostCode = "A11 1AA" },
            Partner = new ParentPartnerData { Nino = "AA987654B", Forename = "Mary", Surname = "Smith" },
            EventDateTime = new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc)
        }
    };

    [SetUp]
    public void Setup()
    {
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockUpsertUseCase = new Mock<IUpsertWorkingFamiliesEventUseCase>(MockBehavior.Strict);
        _mockDeleteUseCase = new Mock<IDeleteWorkingFamiliesEventUseCase>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<EligibilityEventsController>>();

        _sut = new EligibilityEventsController(
            _mockLogger,
            _mockAuditGateway.Object,
            _mockUpsertUseCase.Object,
            _mockDeleteUseCase.Object);
    }

    [TearDown]
    public new void Teardown()
    {
        _mockUpsertUseCase.VerifyAll();
        _mockDeleteUseCase.VerifyAll();
    }

    #region PUT

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn200_WhenUpsertSucceeds()
    {
        // Arrange — spec says 200 OK with no body
        var domain = new WorkingFamiliesEvent { WorkingFamiliesEventID = Guid.NewGuid().ToString() };
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(domain);

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as OkResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn400_WhenModelIsNull()
    {
        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, null!) as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn400_WhenModelStateIsInvalid()
    {
        // Arrange — simulate a model-state error (e.g. dern failed validation)
        _sut.ModelState.AddModelError("Dern", "dern must be exactly 11 characters long");

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn409_WhenDernConflict()
    {
        // Arrange
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ThrowsAsync(new InvalidOperationException("CONFLICT"));

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as ConflictObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn400_WhenValidationExceptionThrown()
    {
        // Arrange
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ThrowsAsync(new ValidationException("Validation failed"));

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn500_WhenUnexpectedExceptionThrown()
    {
        // Arrange
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act — 500 returns a plain-text log GUID
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as ContentResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(500);
        result.ContentType.Should().Be("text/plain");
        Guid.TryParse(result.Content, out _).Should().BeTrue("response body should be a GUID for log searching");
    }

    #endregion

    #region DELETE

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn200_WhenEventDeletedSuccessfully()
    {
        // Arrange
        _mockDeleteUseCase
            .Setup(uc => uc.Execute(ValidHmrcId))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteEligibilityEvent(ValidHmrcId) as OkResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn404_WhenEventNotFound()
    {
        // Arrange
        _mockDeleteUseCase
            .Setup(uc => uc.Execute(ValidHmrcId))
            .ReturnsAsync(false);

        // Act — spec says 404 with no body
        var result = await _sut.DeleteEligibilityEvent(ValidHmrcId) as NotFoundResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn500_WhenUnexpectedExceptionThrown()
    {
        // Arrange
        _mockDeleteUseCase
            .Setup(uc => uc.Execute(ValidHmrcId))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act — 500 returns a plain-text log GUID
        var result = await _sut.DeleteEligibilityEvent(ValidHmrcId) as ContentResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(500);
        result.ContentType.Should().Be("text/plain");
        Guid.TryParse(result.Content, out _).Should().BeTrue("response body should be a GUID for log searching");
    }

    #endregion
}
