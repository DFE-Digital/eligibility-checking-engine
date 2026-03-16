using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace CheckYourEligibility.API.Tests;

[TestFixture]
public class EligibilityEventsControllerTests : TestBase.TestBase
{
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<IUpsertWorkingFamiliesEventUseCase> _mockUpsertUseCase = null!;
    private Mock<IDeleteWorkingFamiliesEventUseCase> _mockDeleteUseCase = null!;
    private Mock<IEcsEligibilityEventsAdapter> _mockEcsAdapter = null!;
    private ILogger<EligibilityEventsController> _mockLogger = null!;
    private IConfiguration _configuration = null!;
    private EligibilityEventsController _sut = null!;

    private const string ValidHmrcId = "21ec3021-31ec-1068-a1dd-08002b40309a";

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
        _mockEcsAdapter = new Mock<IEcsEligibilityEventsAdapter>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<EligibilityEventsController>>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ecs:EligibilityEvents:ForwardToEcs", "true" }
            })
            .Build();

        _sut = new EligibilityEventsController(
            _mockLogger,
            _mockAuditGateway.Object,
            _configuration,
            _mockUpsertUseCase.Object,
            _mockDeleteUseCase.Object,
            _mockEcsAdapter.Object);
    }

    [TearDown]
    public new void Teardown()
    {
        _mockUpsertUseCase.VerifyAll();
        _mockDeleteUseCase.VerifyAll();
        _mockEcsAdapter.VerifyAll();
    }

    #region PUT

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn400_WhenIdIsNotValidGuid()
    {
        // Act
        var result = await _sut.EligibilityEvents("not-a-guid", ValidRequest) as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn200_WhenEcsSucceedsAndUpsertSucceeds()
    {
        // Arrange
        var domain = new WorkingFamiliesEvent { WorkingFamiliesEventID = Guid.NewGuid().ToString() };
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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
    public async Task EligibilityEvents_PUT_ShouldReturn200_WhenNoAdapterConfigured()
    {
        // Arrange — controller without adapter (adapter is optional)
        var sutWithoutAdapter = new EligibilityEventsController(
            _mockLogger,
            _mockAuditGateway.Object,
            _configuration,
            _mockUpsertUseCase.Object,
            _mockDeleteUseCase.Object);

        var domain = new WorkingFamiliesEvent { WorkingFamiliesEventID = Guid.NewGuid().ToString() };
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(domain);

        // Act
        var result = await sutWithoutAdapter.EligibilityEvents(ValidHmrcId, ValidRequest) as OkResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturnEcsStatusCode_WhenEcsReturnsNonSuccess()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("ECS error")
            });

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as ContentResult;

        // Assert — should NOT call upsert use case
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(500);
        result.ContentType.Should().Be("application/json");
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn502_WhenEcsIsUnreachable()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(502);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn502_WhenEcsTimesOut()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(502);
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
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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
    public async Task EligibilityEvents_PUT_ShouldReturn400_WhenDernDatesOverlap()
    {
        // Arrange — ECS succeeds but use case detects DERN date overlap
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ThrowsAsync(new DernOverlapException(
                ValidHmrcId, "50009000005",
                new DateTime(2026, 1, 21), new DateTime(2026, 4, 23),
                new List<OverlapDetail>
                {
                    new OverlapDetail
                    {
                        EligibilityEventId = "other-event-id",
                        Dern = "50009000005",
                        ValidityStartDate = new DateTime(2026, 1, 20),
                        ValidityEndDate = new DateTime(2026, 4, 23)
                    }
                }));

        // Act
        var result = await _sut.EligibilityEvents(ValidHmrcId, ValidRequest) as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task EligibilityEvents_PUT_ShouldReturn400_WhenValidationExceptionThrown()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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
        _mockEcsAdapter
            .Setup(a => a.ForwardPutAsync(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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
    public async Task DeleteEligibilityEvent_ShouldReturn400_WhenIdIsNotValidGuid()
    {
        // Act
        var result = await _sut.DeleteEligibilityEvent("not-a-guid") as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn200_WhenEcsSucceedsAndEventDeleted()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardDeleteAsync(ValidHmrcId))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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
    public async Task DeleteEligibilityEvent_ShouldReturn200_WhenNoAdapterConfigured()
    {
        // Arrange — controller without adapter
        var sutWithoutAdapter = new EligibilityEventsController(
            _mockLogger,
            _mockAuditGateway.Object,
            _configuration,
            _mockUpsertUseCase.Object,
            _mockDeleteUseCase.Object);

        _mockDeleteUseCase
            .Setup(uc => uc.Execute(ValidHmrcId))
            .ReturnsAsync(true);

        // Act
        var result = await sutWithoutAdapter.DeleteEligibilityEvent(ValidHmrcId) as OkResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturnEcsStatusCode_WhenEcsReturnsNonSuccess()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardDeleteAsync(ValidHmrcId))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("ECS unavailable")
            });

        // Act
        var result = await _sut.DeleteEligibilityEvent(ValidHmrcId) as ContentResult;

        // Assert — should NOT call delete use case
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(503);
        result.ContentType.Should().Be("application/json");
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn502_WhenEcsIsUnreachable()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardDeleteAsync(ValidHmrcId))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await _sut.DeleteEligibilityEvent(ValidHmrcId) as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(502);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn502_WhenEcsTimesOut()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardDeleteAsync(ValidHmrcId))
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        // Act
        var result = await _sut.DeleteEligibilityEvent(ValidHmrcId) as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(502);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldReturn404_WhenEventNotFound()
    {
        // Arrange
        _mockEcsAdapter
            .Setup(a => a.ForwardDeleteAsync(ValidHmrcId))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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
        _mockEcsAdapter
            .Setup(a => a.ForwardDeleteAsync(ValidHmrcId))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
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

    [Test]
    public async Task EligibilityEvents_PUT_ShouldSkipEcsForwarding_WhenForwardToEcsIsFalse()
    {
        // Arrange — ForwardToEcs disabled; adapter should NOT be called
        var configOff = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ecs:EligibilityEvents:ForwardToEcs", "false" }
            })
            .Build();
        var sut = new EligibilityEventsController(
            _mockLogger,
            _mockAuditGateway.Object,
            configOff,
            _mockUpsertUseCase.Object,
            _mockDeleteUseCase.Object,
            _mockEcsAdapter.Object);

        var domain = new WorkingFamiliesEvent { WorkingFamiliesEventID = Guid.NewGuid().ToString() };
        _mockUpsertUseCase
            .Setup(uc => uc.Execute(ValidHmrcId, It.IsAny<EligibilityEventRequest>()))
            .ReturnsAsync(domain);

        // Act
        var result = await sut.EligibilityEvents(ValidHmrcId, ValidRequest) as OkResult;

        // Assert — adapter never called, upsert still runs
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        _mockEcsAdapter.Verify(a => a.ForwardPutAsync(It.IsAny<string>(), It.IsAny<EligibilityEventRequest>()), Times.Never);
    }

    [Test]
    public async Task DeleteEligibilityEvent_ShouldSkipEcsForwarding_WhenForwardToEcsIsFalse()
    {
        // Arrange — ForwardToEcs disabled; adapter should NOT be called
        var configOff = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ecs:EligibilityEvents:ForwardToEcs", "false" }
            })
            .Build();
        var sut = new EligibilityEventsController(
            _mockLogger,
            _mockAuditGateway.Object,
            configOff,
            _mockUpsertUseCase.Object,
            _mockDeleteUseCase.Object,
            _mockEcsAdapter.Object);

        _mockDeleteUseCase
            .Setup(uc => uc.Execute(ValidHmrcId))
            .ReturnsAsync(true);

        // Act
        var result = await sut.DeleteEligibilityEvent(ValidHmrcId) as OkResult;

        // Assert — adapter never called, delete still runs
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        _mockEcsAdapter.Verify(a => a.ForwardDeleteAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion
}
