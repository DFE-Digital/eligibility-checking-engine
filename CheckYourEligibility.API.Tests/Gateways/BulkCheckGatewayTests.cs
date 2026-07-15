// Ignore Spelling: Levenshtein

using System.Globalization;
using System.Net;
using AutoFixture;
using AutoMapper;
using Azure.Storage.Queues;
using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using DomainBulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Tests;

public class BulkCheckGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private HashGateway _hashGateway;
    private IMapper _mapper;
    private Mock<IAudit> _moqAudit;
    private Mock<IEcsAdapter> _moqEcsGateway;
    private Mock<IDwpAdapter> _moqDwpGateway;
    private Mock<ICheckEligibility> _moqCheckEligibility;
    private BulkCheckGateway _sut;
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(nameof(BulkCheckGatewayTests), InMemoryDatabaseRoot)
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        // Ensure database is created and clean
        var context = (EligibilityCheckContext)_fakeInMemoryDb;       
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        var configForSmsApi = new Dictionary<string, string>
        {
            { "BulkEligibilityCheckLimit", "250" },
            { "QueueFsmCheckStandard", "notSet" },
            { "QueueFsmCheckBulk", "notSet" },
            { "HashCheckDays", "7" },
            { "Dwp:UseEcsforChecksWF", "false"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _moqCheckEligibility = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);

        _sut = new BulkCheckGateway(new NullLoggerFactory(), _fakeInMemoryDb,
            _moqCheckEligibility.Object,
            _moqAudit.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    #region GetBulkStatuses

    [Test]
    public async Task Given_InvalidLocalAuthorityId_GetBulkStatuses_Should_Return_EmptyList()
    {
        // Arrange – non-numeric LA ID
        var result = await _sut.GetBulkStatuses("not-a-number", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task Given_UnauthorisedUser_GetBulkStatuses_Should_Return_EmptyList()
    {
        // Arrange – user only has access to LA 22, requests LA 201
        var result = await _sut.GetBulkStatuses("201", new List<int> { 22 }, "test-source");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task Given_NoChecksExist_GetBulkStatuses_Should_Return_EmptyList()
    {
        // Arrange – no BulkChecks in DB for this LA
        var result = await _sut.GetBulkStatuses("201", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task Given_BatchWithAllChecksComplete_GetBulkStatuses_Should_Return_CompletedStatus()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();
        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 201,
            SubmittedDate = DateTime.UtcNow.AddDays(-1),
            Filename = "test.csv",
            FinalNameInCheck = "test.csv",
            SubmittedBy = "test@test.com",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            Status = BulkCheckStatus.Completed,
            NumberOfRecords = 2
        });
        _fakeInMemoryDb.CheckEligibilities.AddRange(
            new EligibilityCheck { EligibilityCheckID = Guid.NewGuid().ToString(), BulkCheckID = bulkCheckId, Status = CheckEligibilityStatus.eligible, IsDeleted = false, Type = CheckEligibilityType.FreeSchoolMeals, CheckData = "{}", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Source = "test-source" },
            new EligibilityCheck { EligibilityCheckID = Guid.NewGuid().ToString(), BulkCheckID = bulkCheckId, Status = CheckEligibilityStatus.notEligible, IsDeleted = false, Type = CheckEligibilityType.FreeSchoolMeals, CheckData = "{}", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Source = "test-source" }
        );
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetBulkStatuses("201", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().ContainSingle();
        result!.First().Status.Should().Be(BulkCheckStatus.Completed);
    }

    [Test]
    public async Task Given_BatchWithQueuedChecks_GetBulkStatuses_Should_Return_InProgressStatus()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();
        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 201,
            SubmittedDate = DateTime.UtcNow.AddDays(-1),
            Filename = "test.csv",
            FinalNameInCheck = "test.csv",
            SubmittedBy = "test@test.com",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            NumberOfRecords = 2
        });
        _fakeInMemoryDb.CheckEligibilities.AddRange(
            new EligibilityCheck { EligibilityCheckID = Guid.NewGuid().ToString(), BulkCheckID = bulkCheckId, Status = CheckEligibilityStatus.eligible, IsDeleted = false, Type = CheckEligibilityType.FreeSchoolMeals, CheckData = "{}", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Source = "test-source" },
            new EligibilityCheck { EligibilityCheckID = Guid.NewGuid().ToString(), BulkCheckID = bulkCheckId, Status = CheckEligibilityStatus.queuedForProcessing, IsDeleted = false, Type = CheckEligibilityType.FreeSchoolMeals, CheckData = "{}", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Source = "test-source" }
        );
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetBulkStatuses("201", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().ContainSingle();
        result!.First().Status.Should().Be(BulkCheckStatus.InProgress);
    }

    [Test]
    public async Task Given_NewlySubmittedBatch_WithNoChecksInsertedYet_GetBulkStatuses_Should_Return_InProgressStatus()
    {
        // Arrange – BulkCheck exists but EligibilityCheck rows not yet inserted
        var bulkCheckId = Guid.NewGuid().ToString();
        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 201,
            SubmittedDate = DateTime.UtcNow.AddMinutes(-1),
            Filename = "test.csv",
            FinalNameInCheck = "test.csv",
            SubmittedBy = "test@test.com",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            NumberOfRecords = 100
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetBulkStatuses("201", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().ContainSingle();
        result!.First().Status.Should().Be(BulkCheckStatus.InProgress);
    }

    [Test]
    public async Task Given_BatchWhereAllChecksAreSoftDeleted_GetBulkStatuses_Should_Return_DeletedStatus()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();
        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 201,
            SubmittedDate = DateTime.UtcNow.AddDays(-1),
            Filename = "test.csv",
            FinalNameInCheck = "test.csv",
            SubmittedBy = "test@test.com",
            Status = BulkCheckStatus.Deleted,
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            NumberOfRecords = 2
        });
        _fakeInMemoryDb.CheckEligibilities.AddRange(
            new EligibilityCheck { EligibilityCheckID = Guid.NewGuid().ToString(), BulkCheckID = bulkCheckId, Status = CheckEligibilityStatus.eligible, IsDeleted = true, Type = CheckEligibilityType.FreeSchoolMeals, CheckData = "{}", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Source = "test-source" },
            new EligibilityCheck { EligibilityCheckID = Guid.NewGuid().ToString(), BulkCheckID = bulkCheckId, Status = CheckEligibilityStatus.eligible, IsDeleted = true, Type = CheckEligibilityType.FreeSchoolMeals, CheckData = "{}", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Source = "test-source" }
        );
        await _fakeInMemoryDb.SaveChangesAsync();


        // Act
        var result = await _sut.GetBulkStatuses("201", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().ContainSingle();
        result!.First().Status.Should().Be(BulkCheckStatus.Deleted);
    }

    [Test]
    public async Task Given_BatchOlderThan7Days_GetBulkStatuses_Should_Not_Return_It()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();
        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 201,
            SubmittedDate = DateTime.UtcNow.AddDays(-8),
            Filename = "old.csv",
            FinalNameInCheck = "old.csv",
            SubmittedBy = "test@test.com",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            NumberOfRecords = 1
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act – default includeLast7DaysOnly = true
        var result = await _sut.GetBulkStatuses("201", new List<int> { 201 }, "test-source");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }


    [Test]
    public async Task Given_AdminUser_GetBulkStatuses_Should_Return_ChecksForRequestedLA()
    {
        // Arrange – admin user (allowedLocalAuthorityIds contains 0)
        var bulkCheckId = Guid.NewGuid().ToString();
        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 999,
            SubmittedDate = DateTime.UtcNow.AddDays(-1),
            Filename = "test.csv",
            FinalNameInCheck = "test.csv",
            SubmittedBy = "admin@test.com",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            NumberOfRecords = 1
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetBulkStatuses("999", new List<int> { 0 }, "test-source");

        // Assert
        result.Should().ContainSingle();
        result!.First().BulkCheckID.Should().Be(bulkCheckId);
    }

    [Test]
    public async Task Given_BulkCheck_WithNoChecksAndNoFilename_GetBulkStatuses_Should_Not_Return_Batch()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();

        _fakeInMemoryDb.BulkChecks.Add(new DomainBulkCheck
        {
            BulkCheckID = bulkCheckId,
            LocalAuthorityID = 201,
            SubmittedDate = DateTime.UtcNow,
            Filename = string.Empty,
            FinalNameInCheck = string.Empty,
            SubmittedBy = "test@test.com",
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            NumberOfRecords = 100
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetBulkStatuses(
            "201",
            new List<int> { 201 },
            "test-source");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    [Test]
    public void Given_InValidRequest_GetBulkStatus_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.GetBulkStatus(request);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public void Given_ValidRequest_GetBulkStatus_Should_Return_status()
    {
        // Arrange
        var items = _fixture.CreateMany<EligibilityCheck>();
        var guid = _fixture.Create<string>();
        foreach (var item in items)
        {
            item.Status = CheckEligibilityStatus.queuedForProcessing;
            item.BulkCheckID = guid;
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.Source = "test-source";
            // Set navigation properties to null to avoid creating additional entities
            item.EligibilityCheckHash = null;
            item.EligibilityCheckHashID = null;
            item.BulkCheck = null;
        }
        _fakeInMemoryDb.CheckEligibilities.AddRange(items);
        _fakeInMemoryDb.SaveChanges();
        var results = _fakeInMemoryDb.CheckEligibilities
            .Where(x => x.BulkCheckID == guid)
            .GroupBy(n => n.Status)
            .Select(n => new { Status = n.Key, ct = n.Count() });
        var total = results.Sum(s => s.ct);
        var completed = results.Where(a => a.Status != CheckEligibilityStatus.queuedForProcessing).Sum(s => s.ct);

        // Act
        var response = _sut.GetBulkStatus(guid);

        // Assert
        response.Result.Total.Should().Be(total);
        response.Result.Complete.Should().Be(completed);
    }

    [Test]
    public void Given_InValidRequest_GetBulkCheckResults_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.GetBulkCheckResults<IList<CheckEligibilityItem>>(request);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_GetBulkCheckResults_Should_Return_Items()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var eligibilityCheckId = Guid.NewGuid().ToString();
        var item = _fixture.Create<EligibilityCheck>();
        item.EligibilityCheckID = eligibilityCheckId;
        item.Source = "test-source";
        item.BulkCheckID = groupId;
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData =
            """{"nationalInsuranceNumber": "AB123456C", "lastName": "Something", "dateOfBirth": "2000-01-01", "nationalAsylumSeekerServiceNumber": null}""";
        // Set navigation properties to null to avoid creating additional entities
        item.EligibilityCheckHash = null;
        item.EligibilityCheckHashID = null;
        item.BulkCheck = null;
        item.Status = CheckEligibilityStatus.eligible;
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var changeCount = await _fakeInMemoryDb.SaveChangesAsync();
        changeCount.Should().Be(1);

        // Verify data was saved
        var savedItems = _fakeInMemoryDb.CheckEligibilities.Where(x => x.BulkCheckID == groupId).ToList();
        savedItems.Count.Should().Be(1);

        var items = new CheckEligibilityItem()
        {
            NationalInsuranceNumber = "AB123456C",
            DateOfBirth = "2000-01-01",
            LastName = "SOMETHING"
        };

        _moqCheckEligibility.Setup(x =>
            x.GetItem<CheckEligibilityItem>(It.IsAny<string>(), It.IsAny<CheckEligibilityType>(), It.IsAny<bool>())).ReturnsAsync(items);

        // Act
        var response = await _sut.GetBulkCheckResults<IList<CheckEligibilityItem>>(groupId);

        // Assert
        response.Should().NotBeNull();
        response.Should().BeOfType<List<CheckEligibilityItem>>();
        response.First().DateOfBirth.Should().Contain("2000-01-01");
        response.First().NationalInsuranceNumber.Should().Contain("AB123456C");
        response.First().LastName.Should().Contain("SOMETHING");
        response.First().NationalAsylumSeekerServiceNumber.Should().BeNull();
    }
}
