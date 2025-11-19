using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetBulkUploadProgressUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockBulkCheckGateway = new Mock<IBulkCheck>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetBulkUploadProgressUseCase>>(MockBehavior.Loose);
        _sut = new GetBulkUploadProgressUseCase(_mockBulkCheckGateway.Object, _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockBulkCheckGateway.VerifyAll();
    }

    private Mock<IBulkCheck> _mockBulkCheckGateway;
    private Mock<ILogger<GetBulkUploadProgressUseCase>> _mockLogger;
    private GetBulkUploadProgressUseCase _sut;
    private Fixture _fixture;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(guid);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, group ID is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();
        _mockBulkCheckGateway.Setup(s => s.GetBulkStatus(guid)).ReturnsAsync((BulkStatus)null);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid);

        // Assert
        act.Should().ThrowAsync<NotFoundException>().WithMessage($"Bulk upload with ID {guid} not found");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_status()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusValue = _fixture.Create<BulkStatus>();
        _mockBulkCheckGateway.Setup(s => s.GetBulkStatus(guid)).ReturnsAsync(statusValue);

        // Act
        var result = await _sut.Execute(guid);

        // Assert
        result.Data.Should().Be(statusValue);
        result.Links.Should().NotBeNull();
        result.Links.Get_BulkCheck_Results.Should()
            .Be($"{CheckLinks.BulkCheckLink}{guid}{CheckLinks.BulkCheckResults}");
    }

    [Test]
    public async Task Execute_calls_gateway_GetBulkStatus_with_correct_guid()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var statusValue = _fixture.Create<BulkStatus>();
        _mockBulkCheckGateway.Setup(s => s.GetBulkStatus(guid)).ReturnsAsync(statusValue);

        // Act
        await _sut.Execute(guid);

        // Assert
        _mockBulkCheckGateway.Verify(s => s.GetBulkStatus(guid), Times.Once);
    }
}