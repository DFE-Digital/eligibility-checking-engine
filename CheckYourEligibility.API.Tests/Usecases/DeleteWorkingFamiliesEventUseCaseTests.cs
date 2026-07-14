using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteWorkingFamiliesEventUseCaseTests : TestBase
{
    private Mock<IWorkingFamiliesEvent> _mockGateway = null!;
    private Mock<ILogger<DeleteWorkingFamiliesEventUseCase>> _mockLogger = null!;
    private DeleteWorkingFamiliesEventUseCase _sut = null!;

    private const string ValidHmrcId = "test-hmrc-id-001";

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IWorkingFamiliesEvent>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<DeleteWorkingFamiliesEventUseCase>>(MockBehavior.Loose);
        _sut = new DeleteWorkingFamiliesEventUseCase(_mockGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public new void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_ShouldDeleteEvent_Successfully()
    {
        // Arrange
        _mockGateway.Setup(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId)).ReturnsAsync(true);

        // Act
        var result = await _sut.Execute(ValidHmrcId);

        // Assert
        result.Should().BeTrue();
        _mockGateway.Verify(g => g.DeleteWorkingFamiliesEvent(ValidHmrcId), Times.Once);
    }

    [Test]
    public async Task Execute_ShouldReturnFalse_WhenEventNotFound()
    {
        // Arrange
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


}
