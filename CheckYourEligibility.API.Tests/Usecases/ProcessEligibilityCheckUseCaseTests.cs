using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ProcessEligibilityCheckUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckingEngineGateway = new Mock<ICheckingEngine>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ProcessEligibilityCheckUseCase>>(MockBehavior.Loose);
        _sut = new ProcessEligibilityCheckUseCase(_mockCheckingEngineGateway.Object, _mockAuditGateway.Object,
            _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckingEngineGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<ICheckingEngine> _mockCheckingEngineGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<ProcessEligibilityCheckUseCase>> _mockLogger;
    private ProcessEligibilityCheckUseCase _sut;
    private Fixture _fixture;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(guid);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, check ID is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var auditItemTemplate = _fixture.Create<AuditData>();

        _mockAuditGateway.Setup(a => a.AuditDataGet(AuditType.Check, string.Empty))
            .Returns(auditItemTemplate);
        _mockCheckingEngineGateway.Setup(s => s.ProcessCheck(guid, auditItemTemplate))
            .ReturnsAsync((CheckEligibilityStatus?)null);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage($"Bulk upload with ID {guid} not found");
    }

    [Test]
    public async Task Execute_returns_gatewayUnavailable_when_status_is_queuedForProcessing()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var auditItemTemplate = _fixture.Create<AuditData>();
        var statusValue = _fixture.Create<CheckEligibilityStatus>();

        _mockAuditGateway.Setup(a => a.AuditDataGet(AuditType.Check, string.Empty))
            .Returns(auditItemTemplate);
        _mockCheckingEngineGateway.Setup(s => s.ProcessCheck(guid, auditItemTemplate))
            .ReturnsAsync(statusValue);

        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid)).ReturnsAsync(_fixture.Create<string>());

        // Act
        Func<Task> act = async () => await _sut.Execute(guid);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Service is unavailable");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_status_not_queued()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var auditItemTemplate = _fixture.Create<AuditData>();
        var statusValue = CheckEligibilityStatus.eligible; // Updated to use a specific status

        _mockAuditGateway.Setup(a => a.AuditDataGet(AuditType.Check, string.Empty))
            .Returns(auditItemTemplate);
        _mockCheckingEngineGateway.Setup(s => s.ProcessCheck(guid, auditItemTemplate))
            .ReturnsAsync(statusValue);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid)).ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid);

        // Assert
        result.Data.Status.Should().Be(CheckEligibilityStatus.eligible.ToString());
    }

    [Test]
    public async Task Execute_calls_gateway_ProcessCheck_with_correct_parameters()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var auditItemTemplate = _fixture.Create<AuditData>();
        var statusValue = _fixture.Create<CheckEligibilityStatus>();
        statusValue = CheckEligibilityStatus.eligible;

        _mockAuditGateway.Setup(a => a.AuditDataGet(AuditType.Check, string.Empty))
            .Returns(auditItemTemplate);
        _mockCheckingEngineGateway.Setup(s => s.ProcessCheck(guid, auditItemTemplate))
            .ReturnsAsync(statusValue);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid)).ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid);

        // Assert
        _mockCheckingEngineGateway.Verify(s => s.ProcessCheck(guid, auditItemTemplate), Times.Once);
    }

    [Test]
    public async Task Execute_returns_failure_when_ProcessCheckException_is_thrown()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var auditItemTemplate = _fixture.Create<AuditData>();

        _mockAuditGateway.Setup(a => a.AuditDataGet(AuditType.Check, string.Empty))
            .Returns(auditItemTemplate);
        _mockCheckingEngineGateway.Setup(s => s.ProcessCheck(guid, auditItemTemplate))
            .ThrowsAsync(new ProcessCheckException("Test exception"));

        // Act
        Func<Task> act = async () => await _sut.Execute(guid);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Failed to process eligibility check.");
    }
}