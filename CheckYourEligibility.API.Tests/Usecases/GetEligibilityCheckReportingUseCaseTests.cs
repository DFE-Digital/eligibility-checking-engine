using CheckYourEligibility.API.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

public class GetEligibilityCheckReportingUseCaseTests : TestBase.TestBase
{
    private Mock<IEligibilityCheckReporting> _mockEligibilityCheckReportingGateway;
    private Mock<ILogger<GetEligibilityCheckReportingUseCase>> _mockLogger;
    private Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private Mock<IServiceScope> _mockServiceScope;
    private Mock<IServiceProvider> _mockServiceProvider;
    private GetEligibilityCheckReportingUseCase _sut;

    [SetUp]
    public void Setup()
    {
        _mockEligibilityCheckReportingGateway = new Mock<IEligibilityCheckReporting>();
        _mockLogger = new Mock<ILogger<GetEligibilityCheckReportingUseCase>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IEligibilityCheckReporting))).Returns(_mockEligibilityCheckReportingGateway.Object);
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(_mockServiceScope.Object);

        _sut = new GetEligibilityCheckReportingUseCase(
            _mockEligibilityCheckReportingGateway.Object,
            _mockLogger.Object,
            _mockServiceScopeFactory.Object
        );
    }

    [Test]
    public void Execute_WhenModelIsNull_ThrowsValidationException()
    {
        var ex = Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            async () => await _sut.Execute(null,null));

        Assert.That(ex.Message, Is.EqualTo("Invalid request, model is required"));
    }

    [Test]
    public void Execute_WhenModelIsInvalid_ThrowsFluentValidationException()
    {
        // Arrange
        var invalidModel = new EligibilityCheckReportRequest();

        // Act
        var ex = Assert.ThrowsAsync<FluentValidation.ValidationException>(
            async () => await _sut.Execute(invalidModel, null));

        // Assert
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.Message, Is.Not.Empty);
    }

    [Test]
    public async Task Execute_WhenModelIsValid_ReturnsReportResponseWithNewStatus()
    {
        // Arrange
        var model = new EligibilityCheckReportRequest
        {

            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(-1),
            LocalAuthorityID = 948,
            CheckType = CheckType.AllChecks,
            GeneratedBy = "peterB"
        };

        var reportId = Guid.NewGuid();

        var createdReport = new EligibilityCheckReport
        {
            EligibilityCheckReportId = reportId,
            Status = ReportStatus.New
        };

        _mockEligibilityCheckReportingGateway
            .Setup(g => g.CreateReport(model, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdReport);

        _mockEligibilityCheckReportingGateway
            .Setup(g => g.EligibilityCheckReports(reportId, CheckEligibilityType.FreeSchoolMeals,It.IsAny<string>() ,It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);


        // Act
        var result = await _sut.Execute(model, null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.ReportID, Is.EqualTo(reportId.ToString()));
        Assert.That(result.Data.Status, Is.EqualTo(ReportStatus.New.ToString()));

        _mockEligibilityCheckReportingGateway.Verify(
            g => g.CreateReport(model, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Execute_WhenModelIsValid_TriggersBackgroundReportGeneration()
    {
        // Arrange
        var model = new EligibilityCheckReportRequest
        {
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(-1)
        };

        var reportId = Guid.NewGuid();

        _mockEligibilityCheckReportingGateway
            .Setup(g => g.CreateReport(It.IsAny<EligibilityCheckReportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EligibilityCheckReport
            {
                EligibilityCheckReportId = reportId,
                Status = ReportStatus.New
            });

        _mockEligibilityCheckReportingGateway
        .Setup(g => g.EligibilityCheckReports(reportId, CheckEligibilityType.FreeSchoolMeals,It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(model, null);

        // Give the background Task.Run time to execute
        await Task.Delay(50);

        // Assert
        _mockEligibilityCheckReportingGateway.Verify(
            g => g.EligibilityCheckReports(reportId, CheckEligibilityType.FreeSchoolMeals, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }


}