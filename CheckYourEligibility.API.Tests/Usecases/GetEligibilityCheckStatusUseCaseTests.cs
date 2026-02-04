using AutoFixture;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetEligibilityCheckStatusUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetEligibilityCheckStatusUseCase>>(MockBehavior.Loose);
        _sut = new GetEligibilityCheckStatusUseCase(_mockCheckGateway.Object, _mockAuditGateway.Object,
            _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<ICheckEligibility> _mockCheckGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<GetEligibilityCheckStatusUseCase>> _mockLogger;
    private GetEligibilityCheckStatusUseCase _sut;
    private Fixture _fixture;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Arrange
        var type = _fixture.Create<CheckEligibilityType>();

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, type);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, check ID is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        _mockCheckGateway.Setup(s => s.GetStatus(guid, type)).ReturnsAsync((CheckEligibilityStatus?)null);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, type);

        // Assert
        act.Should().ThrowAsync<NotFoundException>().WithMessage($"Bulk upload with ID {guid} not found");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_status()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var auditId = _fixture.Create<string>();


        var statusValue = _fixture.Create<CheckEligibilityStatus>();
        _mockCheckGateway.Setup(s => s.GetStatus(guid, type)).ReturnsAsync(statusValue);

        var expectedStausCode = CheckEligibilityStatus.queuedForProcessing;
        _mockAuditGateway.Setup(a =>a.CreateAuditEntry(AuditType.Check, guid, null)).ReturnsAsync(auditId);


        // Act
        var result = await _sut.Execute(guid, type);

        // Assert
        result.Data.Should().NotBeNull();
       
        result.Data.Status.Should().Be(expectedStausCode.ToString());
    }

    [Test]
    public async Task Execute_calls_gateway_GetStatus_with_correct_guid()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();
        var type = _fixture.Create<CheckEligibilityType>();
        var statusValue = _fixture.Create<CheckEligibilityStatus>();
        _mockCheckGateway.Setup(s => s.GetStatus(guid, type)).ReturnsAsync(statusValue);

        _mockAuditGateway.Setup(a =>a.CreateAuditEntry(AuditType.Check, guid,null)).ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, type);

        // Assert
        _mockCheckGateway.Verify(s => s.GetStatus(guid, type), Times.Once);
    }
}