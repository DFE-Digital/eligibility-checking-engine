using System.ComponentModel.DataAnnotations;
using AutoFixture;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GenerateEligibilityReportUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GenerateEligibilityCheckReportUseCase>>(MockBehavior.Loose);
        _sut = new GenerateEligibilityCheckReportUseCase(_mockCheckGateway.Object, _mockLogger.Object);
        _fixture = new Fixture();

    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
    }


    private Mock<ICheckEligibility> _mockCheckGateway;
    private Mock<ILogger<GenerateEligibilityCheckReportUseCase>> _mockLogger;
    private GenerateEligibilityCheckReportUseCase _sut;
    private new Fixture _fixture = null!;

    [Test]
    public async Task Execute_returns_failure_when_model_is_null()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(null!);
        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid request, model is required");
    }

    [Test]
    public async Task Execute_returns_failure_when_model_data_is_invalid()
    {
        // Arrange
        var model = new EligibilityCheckReportRequest
        {
            LocalAuthorityID = null, // Invalid data
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-1) // Invalid date range
        };

        // Act
        Func<Task> act = async () => await _sut.Execute(model);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_returns_report_data_when_model_is_valid()
    {
        // Arrange
        var request = _fixture.Build<EligibilityCheckReportRequest>()
        .With(x => x.StartDate, DateTime.UtcNow.AddDays(-2))
        .With(x => x.EndDate, DateTime.UtcNow)
        .With(x => x.LocalAuthorityID, 948)
        .Create();

        var reportItems = _fixture.CreateMany<EligibilityCheckReportItem>(3).ToList();
        var executionResult = new EligibilityCheckReportResponse { Data = reportItems };

        _mockCheckGateway.Setup(u => u.GenerateEligibilityCheckReports(It.IsAny<EligibilityCheckReportRequest>(), It.IsAny<CancellationToken>()))
    .Returns(Task.FromResult<IEnumerable<EligibilityCheckReportItem>>(reportItems));

        // Act
        var response = await _sut.Execute(request);

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().BeEquivalentTo(reportItems);
    }
}