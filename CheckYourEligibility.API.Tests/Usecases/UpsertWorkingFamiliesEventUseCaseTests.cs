using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpsertWorkingFamiliesEventUseCaseTests : TestBase.TestBase
{
    private Mock<IWorkingFamiliesEvent> _mockGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ILogger<UpsertWorkingFamiliesEventUseCase>> _mockLogger = null!;
    private UpsertWorkingFamiliesEventUseCase _sut = null!;

    private const string HmrcId = "test-hmrc-id-001";
    private const string ValidDern = "50009000005";

    private EligibilityEventRequest ValidRequest => new EligibilityEventRequest
    {
        EligibilityEvent = new EligibilityEventData
        {
            Dern = ValidDern,
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
        _mockGateway = new Mock<IWorkingFamiliesEvent>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<UpsertWorkingFamiliesEventUseCase>>(MockBehavior.Loose);
        _sut = new UpsertWorkingFamiliesEventUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public new void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_ShouldThrowConflict_WhenExistingEventHasDifferentDern()
    {
        // Arrange — same HMRC id, but EligibilityCode differs from incoming DERN
        var existing = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            HMRCEligibilityEventId = HmrcId,
            EligibilityCode = "99999999999" // Different DERN
        };
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync(existing);

        // Act
        Func<Task> act = async () => await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("CONFLICT");
    }

    [Test]
    public async Task Execute_ShouldCreateNewEvent_WhenNoExistingEventFound()
    {
        // Arrange
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.Should().NotBeNull();
        result.HMRCEligibilityEventId.Should().Be(HmrcId);
        result.EligibilityCode.Should().Be(ValidDern);
        result.IsDeleted.Should().BeFalse();
        result.DeletedDateTime.Should().BeNull();
    }

    [Test]
    public async Task Execute_ShouldUpdateExistingEvent_WhenSameDernProvided()
    {
        // Arrange
        var existingId = Guid.NewGuid().ToString();
        var existing = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = existingId,
            HMRCEligibilityEventId = HmrcId,
            EligibilityCode = ValidDern,
            CreatedDateTime = new DateTime(2026, 1, 1)
        };
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync(existing);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.WorkingFamiliesEventID.Should().Be(existingId); // Preserves existing PK
        result.CreatedDateTime.Should().Be(existing.CreatedDateTime); // Preserves created date
    }

    [Test]
    public async Task Execute_ShouldSetCreatedDateTime_WhenCreatingNewEvent()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.CreatedDateTime.Should().NotBeNull();
        result.CreatedDateTime.Should().BeAfter(before);
        result.CreatedDateTime.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Test]
    public async Task Execute_ShouldResetSoftDeleteFields_WhenReactivatingDeletedEvent()
    {
        // Arrange — event was previously soft-deleted
        var existing = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            HMRCEligibilityEventId = HmrcId,
            EligibilityCode = ValidDern,
            IsDeleted = true,
            DeletedDateTime = new DateTime(2026, 1, 15),
            CreatedDateTime = new DateTime(2026, 1, 1)
        };
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync(existing);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.IsDeleted.Should().BeFalse();
        result.DeletedDateTime.Should().BeNull();
    }

    [Test]
    public async Task Execute_ShouldMapAllChildFields_WhenCreatingEvent()
    {
        // Arrange
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.ChildFirstName.Should().Be("Charles");
        result.ChildLastName.Should().Be("Smith");
        result.ChildDateOfBirth.Should().Be(new DateTime(2012, 4, 23));
        result.ChildPostCode.Should().Be("A11 1AA");
    }

    [Test]
    public async Task Execute_ShouldMapAllParentFields_WhenCreatingEvent()
    {
        // Arrange
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.ParentFirstName.Should().Be("John");
        result.ParentLastName.Should().Be("Smith");
        result.ParentNationalInsuranceNumber.Should().Be("AA123456A");
    }

    [Test]
    public async Task Execute_ShouldMapPartnerFields_WhenPartnerProvided()
    {
        // Arrange
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.PartnerFirstName.Should().Be("Mary");
        result.PartnerLastName.Should().Be("Smith");
        result.PartnerNationalInsuranceNumber.Should().Be("AA987654B");
    }

    [Test]
    public async Task Execute_ShouldSetEmptyPartnerFields_WhenNoPartnerProvided()
    {
        // Arrange
        var requestNoPartner = new EligibilityEventRequest
        {
            EligibilityEvent = new EligibilityEventData
            {
                Dern = ValidDern,
                SubmissionDate = new DateTime(2026, 1, 20),
                ValidityStartDate = new DateTime(2026, 1, 21),
                ValidityEndDate = new DateTime(2026, 4, 23),
                Parent = new ParentPartnerData { Nino = "AA123456A", Forename = "John", Surname = "Smith" },
                Child = new ChildData { Forename = "Charles", Surname = "Smith", Dob = new DateTime(2012, 4, 23) },
                Partner = null
            }
        };
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, requestNoPartner);

        // Assert
        result.PartnerFirstName.Should().BeEmpty();
        result.PartnerLastName.Should().BeEmpty();
        result.PartnerNationalInsuranceNumber.Should().BeNull();
    }

    [Test]
    public async Task Execute_ShouldPopulateComputedDates_WhenCreatingEvent()
    {
        // Arrange
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert — computed fields must be set (non-default DateTime)
        result.DiscretionaryValidityStartDate.Should().NotBe(default);
        result.GracePeriodEndDate.Should().NotBe(default);
    }

    [Test]
    public async Task Execute_ShouldCallAudit_AfterSuccessfulUpsert()
    {
        // Arrange
        var auditCalled = false;
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty)
            .Callback(() => auditCalled = true);

        // Act
        await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        auditCalled.Should().BeTrue();
    }

    [Test]
    public async Task Execute_ShouldMapEventDateTime_WhenProvided()
    {
        // Arrange
        var eventDt = new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        _mockGateway.Setup(g => g.GetByHMRCId(HmrcId)).ReturnsAsync((WorkingFamiliesEvent?)null);
        _mockGateway.Setup(g => g.UpsertWorkingFamiliesEvent(It.IsAny<WorkingFamiliesEvent>()))
            .ReturnsAsync((WorkingFamiliesEvent wfe) => wfe);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, HmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(HmrcId, ValidRequest);

        // Assert
        result.EventDateTime.Should().Be(eventDt);
    }
}
