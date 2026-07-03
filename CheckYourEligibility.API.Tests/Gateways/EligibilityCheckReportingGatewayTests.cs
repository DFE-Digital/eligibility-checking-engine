
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
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
    [Ignore("Disabled due to using DB in memory")]
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
                Created = DateTime.UtcNow,
                CheckData = GenerateCheckData(CheckEligibilityType.FreeSchoolMeals)
            });
        }

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        await _sut.EligibilityCheckReports(report.EligibilityCheckReportId, CheckEligibilityType.FreeSchoolMeals, CancellationToken.None);

        // Assert
        var updatedReport = await _fakeInMemoryDb.EligibilityCheckReports.SingleAsync();
        updatedReport.NumberOfResults.Should().Be(totalChecks);
    }

    [TestCase("free-school-meals-admin")]
    public async Task EligibilityCheckReports_Should_Classify_Bulk_And_Single_Checks(string source)
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
                BulkCheck = bulkCheck,
                Source = source,
            },
            new EligibilityCheck
            {
                EligibilityCheckID = "SINGLE",
                OrganisationID = 948,
                Created = now,
                Source = source,
                
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


    [Ignore("Disabled due to using DB in memory")]
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
            BulkCheck = null,
            CheckData = GenerateCheckData(CheckEligibilityType.FreeSchoolMeals)
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        await _sut.EligibilityCheckReports(
            report.EligibilityCheckReportId,
            CheckEligibilityType.FreeSchoolMeals,
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
            await _sut.EligibilityCheckReports(Guid.NewGuid(), CheckEligibilityType.FreeSchoolMeals, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task EligibilityCheckReports_Should_Throw_Exception_For_Empty_ReportIdAsync()
    {
        // Arrange
        var emptyReportId = Guid.Empty;

        // Act
        Func<Task> act = async () => await _sut.EligibilityCheckReports(emptyReportId, CheckEligibilityType.FreeSchoolMeals, CancellationToken.None);

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

    #region GetEligibilityCheckReportHistory tests

    [Test]
    public async Task GetEligibilityCheckReportHistory_Should_Throw_For_Null_Or_Whitespace_LocalAuthorityId()
    {
        // Act
        Func<Task> act = async () =>
            await _sut.GetEligibilityCheckReportHistory("", pageNumber: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task GetEligibilityCheckReportHistory_Should_Return_Empty_Data_When_No_Reports_Exist()
    {
        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 1);

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().BeEmpty();
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(10);
        response.TotalNumberOfRecords.Should().Be(0);
    }

    [Test]
    public async Task GetEligibilityCheckReportHistory_Should_Return_Single_Report()
    {
        // Arrange
        _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
        {
            LocalAuthorityID = 948,
            ReportGeneratedDate = DateTime.UtcNow,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            GeneratedBy = "peterB",
            NumberOfResults = 15,
            Status = ReportStatus.Complete
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 1);

        // Assert
        response.TotalNumberOfRecords.Should().Be(1);
        response.Data.Should().HaveCount(1);

        var item = response.Data.Single();
        item.ReportID.Should().NotBeNullOrWhiteSpace();
        item.GeneratedBy.Should().Be("peterB");
        item.NumberOfResults.Should().Be(15);
        item.Status.Should().Be(ReportStatus.Complete.ToString());
    }

    [Test]
    public async Task GetEligibilityCheckReportHistory_Should_Return_Paginated_Results()
    {
        // Arrange
        for (int i = 0; i < 25; i++)
        {
            _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
            {
                LocalAuthorityID = 948,
                ReportGeneratedDate = DateTime.UtcNow.AddMinutes(-i),
                GeneratedBy = $"user{i}",
                Status = ReportStatus.Complete
            });
        }

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 2);

        // Assert
        response.TotalNumberOfRecords.Should().Be(25);
        response.PageNumber.Should().Be(2);
        response.PageSize.Should().Be(10);
        response.Data.Should().HaveCount(10);
    }

    [Test]
    public async Task GetEligibilityCheckReportHistory_PageNumber_Less_Than_One_Should_Default_To_One()
    {
        // Arrange
        _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
        {
            LocalAuthorityID = 948,
            ReportGeneratedDate = DateTime.UtcNow,
            Status = ReportStatus.Complete,
            GeneratedBy = "peterB"
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 0);

        // Assert
        response.PageNumber.Should().Be(1);
        response.Data.Should().HaveCount(1);
    }

    [Test]
    public async Task GetEligibilityCheckReportHistory_PageNumber_Exceeds_Max_Should_Return_Last_Page()
    {
        // Arrange (15 records → 2 pages)
        for (int i = 0; i < 15; i++)
        {
            _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
            {
                LocalAuthorityID = 948,
                ReportGeneratedDate = DateTime.UtcNow.AddMinutes(-i),
                GeneratedBy = "peterb",
                Status = ReportStatus.Complete
            });
        }

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 99);

        // Assert
        response.PageNumber.Should().Be(2);
        response.Data.Should().HaveCount(5);
    }

    [Test]
    public async Task GetEligibilityCheckReportHistory_Should_Order_By_ReportGeneratedDate_Descending()
    {
        // Arrange
        var older = DateTime.UtcNow.AddDays(-1);
        var newer = DateTime.UtcNow;

        _fakeInMemoryDb.EligibilityCheckReports.AddRange(
            new EligibilityCheckReport
            {
                LocalAuthorityID = 948,
                ReportGeneratedDate = older,
                Status = ReportStatus.Complete,
                GeneratedBy = "peterB",
                StartDate = older.AddDays(-7),
                EndDate = older.AddDays(7)

            },
            new EligibilityCheckReport
            {
                LocalAuthorityID = 948,
                ReportGeneratedDate = newer,
                Status = ReportStatus.Complete,
                GeneratedBy = "peterB",
                StartDate = newer.AddDays(-7),
                EndDate = newer.AddDays(7)
            });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 1);

        // Assert
        response.Data.First().ReportGeneratedDate.Should().Be(newer);
    }

    #endregion

    #region GetLocalAuthorityIdForReport tests

    [Test]
    public async Task GetLocalAuthorityIdForReport_Should_Return_LocalAuthorityId_When_Report_Exists()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var expectedLocalAuthorityId = 948;

        _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
        {
            EligibilityCheckReportId = reportId,
            LocalAuthorityID = expectedLocalAuthorityId,
            ReportGeneratedDate = DateTime.UtcNow,
            GeneratedBy = "peterB",
            Status = ReportStatus.Complete
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetLocalAuthorityIdForReport(reportId, CancellationToken.None);

        // Assert
        result.Should().Be(expectedLocalAuthorityId);
    }

    [Test]
    public void GetLocalAuthorityIdForReport_Should_Throw_For_EmptyGuid()
    {
        // Act & Assert
        Func<Task> act = async () => await _sut.GetLocalAuthorityIdForReport(Guid.Empty, CancellationToken.None);

        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void GetLocalAuthorityIdForReport_Should_Throw_NotFoundException_When_Report_Not_Found()
    {
        // Act & Assert
        Func<Task> act = async () => await _sut.GetLocalAuthorityIdForReport(Guid.NewGuid(), CancellationToken.None);

        act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Eligibility report not found");
    }

    #endregion

    #region DeleteEligibilityCheckReport tests

    [Test]
    public async Task DeleteEligibilityCheckReport_Should_Soft_Delete_Report()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        var report = new EligibilityCheckReport
        {
            EligibilityCheckReportId = reportId,
            LocalAuthorityID = 948,
            ReportGeneratedDate = DateTime.UtcNow,
            GeneratedBy = "peterB",
            Status = ReportStatus.Complete,
            IsDeleted = false
        };

        _fakeInMemoryDb.EligibilityCheckReports.Add(report);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        await _sut.DeleteEligibilityCheckReport(reportId, CancellationToken.None);

        // Assert
        var deletedReport = await _fakeInMemoryDb.EligibilityCheckReports
            .FirstAsync(r => r.EligibilityCheckReportId == reportId);

        deletedReport.IsDeleted.Should().BeTrue();
    }

    [Test]
    public async Task DeleteEligibilityCheckReport_Should_Not_Return_Deleted_Reports_In_History()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        _fakeInMemoryDb.EligibilityCheckReports.Add(new EligibilityCheckReport
        {
            EligibilityCheckReportId = reportId,
            LocalAuthorityID = 948,
            ReportGeneratedDate = DateTime.UtcNow,
            GeneratedBy = "peterB",
            Status = ReportStatus.Complete,
            IsDeleted = false
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act - Delete the report
        await _sut.DeleteEligibilityCheckReport(reportId, CancellationToken.None);

        // Act - Query history
        var response = await _sut.GetEligibilityCheckReportHistory("948", pageNumber: 1);

        // Assert
        response.TotalNumberOfRecords.Should().Be(0);
        response.Data.Should().BeEmpty();
    }

    [Test]
    public void DeleteEligibilityCheckReport_Should_Throw_For_EmptyGuid()
    {
        // Act & Assert
        Func<Task> act = async () => await _sut.DeleteEligibilityCheckReport(Guid.Empty, CancellationToken.None);

        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void DeleteEligibilityCheckReport_Should_Throw_NotFoundException_When_Report_Not_Found()
    {
        // Act & Assert
        Func<Task> act = async () => await _sut.DeleteEligibilityCheckReport(Guid.NewGuid(), CancellationToken.None);

        act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Eligibility report not found");
    }

    #endregion

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

    private string GenerateCheckData(CheckEligibilityType eligibilityCheckType)
    {
        var checkData = new
        {
            ClientIdentifier = (string?)null,
            NationalAsylumSeekerServiceNumber = "",
            DateOfBirth = "1993-09-17",
            LastName = "TESTER",
            Type = eligibilityCheckType.ToString(),
            NationalInsuranceNumber = "NN128618D"
        };

        return JsonConvert.SerializeObject(checkData);
    }
    #endregion
}


