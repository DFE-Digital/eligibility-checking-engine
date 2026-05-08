
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework.Internal;

namespace CheckYourEligibility.API.Tests.Gateways;

public class EligibilityCheckReportingGatewayTests : TestBase.TestBase
{
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

        _sut = new EligibilityCheckReportingGateway(_fakeInMemoryDb, _mockLogger.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task EligibilityCheckReports_Should_Process_Checks_In_Batches()
    {
        // Arrange
        const int totalChecks = 25_000; 
        var report = new EligibilityCheckReport
        {
            EligibilityCheckReportId = Guid.NewGuid(),
            LocalAuthorityID = 948,
            CheckType = CheckType.AllChecks,
            GeneratedBy = "peterB",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
        };

        _fakeInMemoryDb.EligibilityCheckReports.Add(report);

        for (int i = 0; i < totalChecks; i++)
        {
            _fakeInMemoryDb.CheckEligibilities.Add(new EligibilityCheck
            {
                EligibilityCheckID = $"CHK{i:D2}",
                OrganisationID = 948,
                Created = DateTime.UtcNow
            });
        }

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        await _sut.EligibilityCheckReports(report.EligibilityCheckReportId);

        // Assert
        var updatedReport = await _fakeInMemoryDb.EligibilityCheckReports.SingleAsync();
        updatedReport.NumberOfResults.Should().Be(totalChecks);
    }

    [Test]
    public async Task EligibilityCheckReports_Should_Classify_Bulk_And_Single_Checks()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var report = new EligibilityCheckReport
        {
            EligibilityCheckReportId = Guid.NewGuid(),
            LocalAuthorityID = 948,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            CheckType = CheckType.AllChecks,
            GeneratedBy = "peterB",
        };

        var bulkCheck = new BulkCheck { BulkCheckID = "B1" };

        _fakeInMemoryDb.EligibilityCheckReports.Add(report);

        _fakeInMemoryDb.CheckEligibilities.AddRange(
            new EligibilityCheck
            {
                EligibilityCheckID = "BULK",
                OrganisationID = 948,
                Created = now,
                BulkCheck = bulkCheck
            },
            new EligibilityCheck
            {
                EligibilityCheckID = "SINGLE",
                OrganisationID = 948,
                Created = now
            });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var query = _sut.GetCheckQuery(report);

        var results = await query
            .Select(e => new
            {
                e.EligibilityCheckID,
                IsBulk = e.BulkCheck != null
            })
            .ToListAsync();

        // Assert
        results.Should().ContainSingle(r =>
            r.EligibilityCheckID == "BULK" && r.IsBulk);

        results.Should().ContainSingle(r =>
            r.EligibilityCheckID == "SINGLE" && !r.IsBulk);
    }

    [Test]
    public async Task EligibilityCheckReports_Should_Set_Status_To_Generating_And_Complete()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var report = new EligibilityCheckReport
        {
            EligibilityCheckReportId = Guid.NewGuid(),
            LocalAuthorityID = 948,
            GeneratedBy = "peterB",
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            Status = ReportStatus.New
        };

        _fakeInMemoryDb.EligibilityCheckReports.Add(report);

        _fakeInMemoryDb.CheckEligibilities.Add(new EligibilityCheck
        {
            EligibilityCheckID = "CHK001",
            OrganisationID = 948,
            Created = now,
            BulkCheck = null             // ✅ individual check
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        await _sut.EligibilityCheckReports(
            report.EligibilityCheckReportId,
            CancellationToken.None);

        // Assert
        var updated = await _fakeInMemoryDb.EligibilityCheckReports
            .FirstAsync(r => r.EligibilityCheckReportId == report.EligibilityCheckReportId);

        updated.Status.Should().Be(ReportStatus.Complete);
        updated.NumberOfResults.Should().Be(1);
    }

    [Test]
    public async Task EligibilityCheckReports_ReportNotFound_Throws()
    {
        Func<Task> act = async () =>
            await _sut.EligibilityCheckReports(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task EligibilityCheckReports_Should_Throw_Exception_For_Empty_ReportIdAsync()
    {
        // Arrange
        var emptyReportId = Guid.Empty;

        // Act
        Func<Task> act = async () => await _sut.EligibilityCheckReports(emptyReportId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task SaveEligibilityCheckReport_Should_Save_Report_To_DatabaseAsync()
    {
        // Arrange
        var report = new EligibilityCheckReportRequest
        {
            LocalAuthorityID = 948,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(7),
            GeneratedBy = "peterB",
        };

        // Act
        await _sut.CreateReport(report, cancellationToken: CancellationToken.None);

        // Assert
        var savedReport = await _fakeInMemoryDb.EligibilityCheckReports.FirstOrDefaultAsync(r => r.LocalAuthorityID == 948);
        savedReport.Should().NotBeNull();
        savedReport!.LocalAuthorityID.Should().Be(948);
        savedReport.GeneratedBy.Should().Be("peterB");
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


