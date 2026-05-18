using CheckYourEligibility.API.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetEligibilityReportStatusUseCaseTests : TestBase.TestBase
{
    private Mock<IEligibilityCheckReporting> _mockGateway;
    private Mock<ILogger<GetEligibilityReportStatusUseCase>> _mockLogger;
    private GetEligibilityReportStatusUseCase _sut;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IEligibilityCheckReporting>();
        _mockLogger = new Mock<ILogger<GetEligibilityReportStatusUseCase>>();
        _sut = new GetEligibilityReportStatusUseCase(_mockGateway.Object, _mockLogger.Object);
    }

    [Test]
    public void Execute_InvalidGuid_ThrowsValidationException()
    {
        var invalidId = "not-a-guid";
        var ex = Assert.ThrowsAsync<ValidationException>(async () => await _sut.Execute(invalidId));
        Assert.That(ex.Message, Is.EqualTo("Invalid report ID format. Must be a GUID"));
    }

    [Test]
    public void Execute_ReportNotFound_ThrowsNotFoundException()
    {
        var guid = Guid.NewGuid();
        _mockGateway.Setup(g => g.GetEligibilityReportById(guid)).ReturnsAsync((EligibilityCheckReport)null);
        Assert.ThrowsAsync<NotFoundException>(async () => await _sut.Execute(guid.ToString()));
    }

    [Test]
    public async Task Execute_ReportFound_ReturnsStatusResponse()
    {
        var guid = Guid.NewGuid();
        var report = new EligibilityCheckReport { Status = ReportStatus.Complete };
        _mockGateway.Setup(g => g.GetEligibilityReportById(guid)).ReturnsAsync(report);
        var result = await _sut.Execute(guid.ToString());
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(ReportStatus.Complete.ToString()));
    }

    [Test]
    public async Task Execute_ReportFoundWithNullStatus_ReturnsArchivedStatus()
    {
        var guid = Guid.NewGuid();
        var report = new EligibilityCheckReport { Status = null };
        _mockGateway.Setup(g => g.GetEligibilityReportById(guid)).ReturnsAsync(report);
        var result = await _sut.Execute(guid.ToString());
        Assert.That(result.Status, Is.EqualTo(ReportStatus.Archived.ToString()));
    }
}
