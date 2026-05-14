using AutoFixture;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteEligibilityCheckReportUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockReportingGateway = new Mock<IEligibilityCheckReporting>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<DeleteEligibilityCheckReportUseCase>>();
        _sut = new DeleteEligibilityCheckReportUseCase(_mockReportingGateway.Object, _mockLogger);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockReportingGateway.VerifyAll();
    }

    private Mock<IEligibilityCheckReporting> _mockReportingGateway = null!;
    private ILogger<DeleteEligibilityCheckReportUseCase> _mockLogger = null!;
    private DeleteEligibilityCheckReportUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public async Task Execute_ValidGuidWithAllPermissions_DeletesReportSuccessfully()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var localAuthorityIds = new List<int> { 0 }; // 0 means "all" permissions
        var reportLocalAuthorityId = _fixture.Create<int>();

        _mockReportingGateway.Setup(x => x.GetLocalAuthorityIdForReport(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportLocalAuthorityId);
        _mockReportingGateway.Setup(x => x.DeleteEligibilityCheckReport(reportId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(reportId, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
    }

    [Test]
    public async Task Execute_ValidGuidWithMatchingLocalAuthority_DeletesReportSuccessfully()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reportLocalAuthorityId = 123;
        var localAuthorityIds = new List<int> { 123, 456 }; // Contains matching authority

        _mockReportingGateway.Setup(x => x.GetLocalAuthorityIdForReport(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportLocalAuthorityId);
        _mockReportingGateway.Setup(x => x.DeleteEligibilityCheckReport(reportId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(reportId, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
    }

    [Test]
    public void Execute_ValidGuidWithNonMatchingLocalAuthority_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reportLocalAuthorityId = 999;
        var localAuthorityIds = new List<int> { 123, 456 }; // Does not contain 999

        _mockReportingGateway.Setup(x => x.GetLocalAuthorityIdForReport(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportLocalAuthorityId);

        // Act & Assert
        var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _sut.Execute(reportId, localAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("You do not have permission to delete reports for this local authority");
    }

    [Test]
    public void Execute_ReportNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var localAuthorityIds = new List<int> { 0 }; // "all" permissions

        _mockReportingGateway.Setup(x => x.GetLocalAuthorityIdForReport(reportId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Eligibility report not found"));

        // Act & Assert
        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await _sut.Execute(reportId, localAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("Eligibility report not found");
    }

    [Test]
    public void Execute_EmptyLocalAuthorityList_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var localAuthorityIds = new List<int>(); // Empty list

        // No mock setup needed since UnauthorizedAccessException should be thrown immediately

        // Act & Assert
        var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _sut.Execute(reportId, localAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("You do not have permission to delete reports for this local authority");
    }

    [Test]
    public void Execute_NullLocalAuthorityList_ThrowsArgumentNullException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        List<int> localAuthorityIds = null!;

        // No mock setup needed since ArgumentNullException should be thrown immediately

        // Act & Assert
        var exception = Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sut.Execute(reportId, localAuthorityIds));

        exception.Should().NotBeNull();
    }

    [Test]
    public void Execute_EmptyGuid_ThrowsArgumentNullException()
    {
        // Arrange
        var reportId = Guid.Empty;
        var localAuthorityIds = new List<int> { 0 };

        // No mock setup needed since ArgumentNullException should be thrown immediately

        // Act & Assert
        var exception = Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sut.Execute(reportId, localAuthorityIds));

        exception.Should().NotBeNull();
    }
}
