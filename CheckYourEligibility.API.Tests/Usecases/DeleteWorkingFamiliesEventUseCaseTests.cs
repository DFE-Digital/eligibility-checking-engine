using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteWorkingFamiliesEventUseCaseTests : TestBase.TestBase
{
    private Mock<IWorkingFamiliesEvent> _mockGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ILogger<DeleteWorkingFamiliesEventUseCase>> _mockLogger = null!;
    private DeleteWorkingFamiliesEventUseCase _sut = null!;

    private const string ValidHmrcId = "test-hmrc-id-001";

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IWorkingFamiliesEvent>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<DeleteWorkingFamiliesEventUseCase>>(MockBehavior.Loose);
        _sut = new DeleteWorkingFamiliesEventUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public new void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_ShouldReturnTrue_WhenEventExistsAndIsDeleted()
    {
        // Arrange
        _mockGateway.Setup(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId)).ReturnsAsync(true);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, ValidHmrcId, null))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Execute(ValidHmrcId);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task Execute_ShouldReturnFalse_WhenEventDoesNotExist()
    {
        // Arrange
        _mockGateway.Setup(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId)).ReturnsAsync(false);

        // Act
        var result = await _sut.Execute(ValidHmrcId);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task Execute_ShouldReturnFalse_WhenEventAlreadySoftDeleted()
    {
        // Arrange — gateway returns false when record is already deleted (not found with IsDeleted=false)
        _mockGateway.Setup(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId)).ReturnsAsync(false);

        // Act
        var result = await _sut.Execute(ValidHmrcId);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Execute_ShouldThrowArgumentNullException_WhenIdIsNull()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(null!);

        // Assert
        act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [Test]
    public void Execute_ShouldThrowArgumentNullException_WhenIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(string.Empty);

        // Assert
        act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [Test]
    public void Execute_ShouldThrowArgumentNullException_WhenIdIsWhitespace()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute("   ");

        // Assert
        act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_ShouldCallAudit_WhenEventSuccessfullyDeleted()
    {
        // Arrange
        var auditCalled = false;
        _mockGateway.Setup(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId)).ReturnsAsync(true);
        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.WorkingFamilies, ValidHmrcId, null))
            .ReturnsAsync(string.Empty)
            .Callback(() => auditCalled = true);

        // Act
        await _sut.Execute(ValidHmrcId);

        // Assert
        auditCalled.Should().BeTrue();
    }

    [Test]
    public async Task Execute_ShouldNotCallAudit_WhenEventNotFound()
    {
        // Arrange
        _mockGateway.Setup(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId)).ReturnsAsync(false);

        // Act
        await _sut.Execute(ValidHmrcId);

        // Assert — audit should NOT be called (VerifyAll on _mockAuditGateway confirms no unexpected calls)
        _mockAuditGateway.VerifyNoOtherCalls();
    }
}
