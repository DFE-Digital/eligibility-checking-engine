
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework.Internal;

namespace CheckYourEligibility.API.Tests.Gateways;

public class EligibilityCheckReportingGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;

    private Mock<ILogger<EligibilityCheckReportingGateway>> _mockLogger = null!;

    private EligibilityCheckReportingGateway _sut;

    [SetUp]
    public async Task SetUpAsync()
    {
        var databaseName = $"FakeInMemoryDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        _mockLogger = new Mock<ILogger<EligibilityCheckReportingGateway>>();

        // Ensure database is created and clean
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureCreatedAsync();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        _sut = new EligibilityCheckReportingGateway(_configuration, _fakeInMemoryDb, _mockLogger.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task EligibilityReport_Should_Throw_Exception_When_No_Matching_Bulk_Checks()
    {
        // Arrange
        var request = new EligibilityCheckReportRequest
        {
            LocalAuthorityID = null,
            StartDate = DateTime.UtcNow.AddDays(1), // Future date
            EndDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        Func<Task> act = async () => await _sut.EligibilityCheckReports(request);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task EligibilityReport_Should_Return_Report_When_Matching_Bulk_Checks_Exist()
    {
        // Arrange
        var reportRequest = _fixture.Create<EligibilityCheckReportRequest>();
        reportRequest.StartDate = DateTime.UtcNow.AddDays(-7);
        reportRequest.EndDate = DateTime.UtcNow.AddDays(7);
        reportRequest.LocalAuthorityID = 948; // Ensure it matches all records

        // 3 bulk checks with 5 eligibility checks each should be sufficient to test the report generation and performance with multiple records
        for (var i = 0; i < 3; i++)
        {
            var item = GetBulkCheckWithEligibilityChecks(5, CheckEligibilityType.FreeSchoolMeals, 948).EligibilityChecks.First();
            _fakeInMemoryDb.BulkChecks.Add(item.BulkCheck);

            await _fakeInMemoryDb.SaveChangesAsync();
        }

        // Act
        var response = await _sut.EligibilityCheckReports(reportRequest);

        // Assert
        response.Should().BeAssignableTo<IEnumerable<EligibilityCheckReportResponseItem>>();
        response.Should().NotBeNull();
        response.Count().Should().Be(15); // 3 bulk checks with 5 eligibility checks each should result in 15 report items
    }

    [Test]
    public async Task EligibilityReport_Should_Return_Empty_Report_When_No_Eligibility_Checks_Found()
    {
        // Arrange
        var reportRequest = _fixture.Create<EligibilityCheckReportRequest>();
        reportRequest.StartDate = DateTime.UtcNow.AddDays(-7);
        reportRequest.EndDate = DateTime.UtcNow.AddDays(7);
        reportRequest.LocalAuthorityID = 948; // Ensure it matches all records

        // Add a bulk check with no eligibility checks to test the scenario where bulk checks exist but no eligibility checks are found
        var bulkCheck = new Domain.BulkCheck
        {
            BulkCheckID = Guid.NewGuid().ToString(),
            Filename = "test.csv",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            LocalAuthorityID = 948,
            SubmittedDate = DateTime.UtcNow,
            Status = BulkCheckStatus.InProgress,
            EligibilityChecks = new List<EligibilityCheck>() // No eligibility checks
        };
        _fakeInMemoryDb.BulkChecks.Add(bulkCheck);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.EligibilityCheckReports(reportRequest);

        // Assert
        response.Should().BeAssignableTo<IEnumerable<EligibilityCheckReportResponseItem>>();
        response.Should().NotBeNull();
        response.Count().Should().Be(0); // No eligibility checks should result in an empty report
    }

    [Test]
    public async Task GetReportHistory_Should_Return_Report_History_For_Local_AuthorityAsync()
    {
        // Arrange
        _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
        {
            LocalAuthorityID = 948,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(7),
            GeneratedBy = "peterB",
            NumberOfResults = 15
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948");

        // Assert
        response.Should().BeAssignableTo<IEnumerable<EligibilityCheckReportHistoryItem>>();
        response.Count().Should().Be(1);  // only one report record been added.
    }

    [Test]
    public async Task GetReportHistory_Should_Return_Empty_History_When_No_Reports_FoundAsync()
    {
        // Arrange
        // No reports added to the database to ensure it returns an empty history

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948");

        // Assert
        response.Should().BeAssignableTo<IEnumerable<EligibilityCheckReportHistoryItem>>();
        response.Count().Should().Be(0);  // No reports should result in an empty history
    }

    [Test]
    public async Task GetReportHistory_Should_Throw_Exception_For_Empty_Local_AuthorityIdAsync()
    {
        // Arrange
        var empty = "";

        // Act
        Func<Task> act = async () => await _sut.GetEligibilityCheckReportHistory(empty);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    #region Helper Methods
    private BulkCheck GetBulkCheckWithEligibilityChecks(int numberOfChecks, CheckEligibilityType type, int localAuthorityId)
    {
        var bulkCheck = new BulkCheck
        {
            BulkCheckID = Guid.NewGuid().ToString(),
            Filename = "test.csv",
            EligibilityType = type,
            LocalAuthorityID = localAuthorityId,
            SubmittedDate = DateTime.UtcNow,
            Status = BulkCheckStatus.InProgress,
            EligibilityChecks = new List<EligibilityCheck>()
        };

        for (var i = 0; i < numberOfChecks; i++)
        {
            var request = _fixture.Create<CheckEligibilityRequestData>();
            request.DateOfBirth = DateTime.UtcNow.AddYears(-18).ToString("yyyy-MM-dd"); // Always valid date
            var eligibilityCheck = new EligibilityCheck
            {
                EligibilityCheckID = Guid.NewGuid().ToString(),
                Type = type,
                Status = CheckEligibilityStatus.eligible,
                CheckData = JsonConvert.SerializeObject(GetCheckProcessData(request)),
                BulkCheckID = bulkCheck.BulkCheckID, // Set FK
                BulkCheck = bulkCheck                // Set navigation property
            };
            bulkCheck.EligibilityChecks.Add(eligibilityCheck);
        }

        return bulkCheck;
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth ?? "1990-01-01",
            LastName = request.LastName,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = request.Type
        };
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestWorkingFamiliesData request)
    {
        return new CheckProcessData
        {
            EligibilityCode = request.EligibilityCode,
            LastName = request.LastName,
            GracePeriodEndDate = request.GracePeriodEndDate,
            ValidityStartDate = request.ValidityStartDate,
            ValidityEndDate = request.ValidityEndDate,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            DateOfBirth = request.DateOfBirth,
            Type = CheckEligibilityType.WorkingFamilies
        };
    }
    #endregion
}


    