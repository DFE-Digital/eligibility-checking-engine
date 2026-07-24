using System.ComponentModel.DataAnnotations;
using AutoFixture;
using CheckYourEligibility.Core.Boundary.Responses;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetEligibilityReportHistoryUseCaseTests : TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckGateway = new Mock<IEligibilityCheckReporting>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetEligibilityReportHistoryUseCase>>(MockBehavior.Loose);
        _sut = new GetEligibilityReportHistoryUseCase(_mockCheckGateway.Object, _mockLogger.Object);

        _localAuth = "948";

        _validReportHistory = new EligibilityCheckReportHistoryResponse
        {
            Data = new List<EligibilityCheckReportHistoryItem>
            {
                new EligibilityCheckReportHistoryItem
                {
                    ReportGeneratedDate = DateTime.UtcNow.AddDays(-10),
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow.AddDays(-20),
                    GeneratedBy = "User1",
                    NumberOfResults = 5
                },
                new EligibilityCheckReportHistoryItem
                {
                    ReportGeneratedDate = DateTime.UtcNow.AddDays(-5),
                    StartDate = DateTime.UtcNow.AddDays(-15),
                    EndDate = DateTime.UtcNow.AddDays(-10),
                    GeneratedBy = "User2",
                    NumberOfResults = 3
                }
            }
        };

    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
    }

    private Mock<IEligibilityCheckReporting> _mockCheckGateway;
    private Mock<ILogger<GetEligibilityReportHistoryUseCase>> _mockLogger;
    private GetEligibilityReportHistoryUseCase _sut;
    private string _localAuth = null!;
    private EligibilityCheckReportHistoryResponse _validReportHistory = null!;

    [Test]
    public async Task Execute_WhenLocalAuthorityIdIsProvided_ReturnsReportHistory()
    {
        // Arrange
        var reportItems = _fixture.CreateMany<EligibilityCheckReportHistoryItem>(3).ToList();
        var executionResult = new EligibilityCheckReportHistoryResponse { Data = reportItems };
        var localAuthorityIds = new List<int> { 948 };
        int pageNumber = 1;

        _ = _mockCheckGateway.Setup(u => u.GetEligibilityCheckReportHistory(_localAuth, pageNumber))
    .Returns(Task.FromResult<EligibilityCheckReportHistoryResponse>(executionResult));



         // Act
        var response = await _sut.Execute(_localAuth, localAuthorityIds, pageNumber);

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().BeEquivalentTo(reportItems);
    }

    [Test]
    public async Task Execute_WhenLocalAuthorityIdIsNotInScope_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var localAuthorityId = "999";  
        var localAuthorityIds = new List<int> { 201 }; 

        // Act + Asserts
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _sut.Execute(localAuthorityId, localAuthorityIds, pageNumber: 1));
    }

    [Test]
    public async Task Execute_WhenLocalAuthorityIdIsNullOrEmpty_ThrowsValidationException()
    {
        var localAuthorityId = string.Empty;
        var localAuthorityIds = new List<int> { 201 };

        Assert.ThrowsAsync<ValidationException>(async () => await _sut.Execute(localAuthorityId, localAuthorityIds, pageNumber: 1));
    }
}