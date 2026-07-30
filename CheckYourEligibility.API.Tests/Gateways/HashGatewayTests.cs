// Ignore Spelling: Levenshtein

using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class HashGatewayTests : TestBase.TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    private readonly int _hashCheckDays = 7;
    private readonly int _hashCheckDaysWF = 1;
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private Mock<IAudit> _moqAudit;
    private HashGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(nameof(HashGatewayTests), InMemoryDatabaseRoot)
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);
        _fakeInMemoryDb.Database.EnsureDeleted();
        _fakeInMemoryDb.Database.EnsureCreated();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var configForSmsApi = new Dictionary<string, string>
        {
            { "QueueFsmCheckStandard", "notSet" },
            { "HashCheckDays", _hashCheckDays.ToString() },
            { "HashCheckDaysWF", _hashCheckDaysWF.ToString() },
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();

        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new HashGateway(
            new NullLoggerFactory(),
            _fakeInMemoryDb,
            _configuration,
            _moqAudit.Object);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public async Task Given_validRequest_Create_Exists_Should_Return_Hash()
    {
        // Arrange
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);

        // Act
        var id = await _sut.Create(
            dataItem,
            CheckEligibilityStatus.parentNotFound,
            null,
            ProcessEligibilityCheckSource.HMRC);

        await _fakeInMemoryDb.SaveChangesAsync();

        var response = await _sut.Exists(dataItem);

        // Assert
        response.Should().BeOfType<EligibilityCheckHash>();
    }

    [Test]
    public async Task Given_validRequest_Create_Exists_Should_Return_Hash_WorkingFamilies()
    {
        // Arrange
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var wf = _fixture.Create<CheckEligibilityRequestData>();
        wf.DateOfBirth = "1990-01-01";
        wf.Type = CheckEligibilityType.WorkingFamilies;
        var dataItem = GetCheckProcessData(wf);

        // Act
        var id = await _sut.Create(
            dataItem,
            CheckEligibilityStatus.parentNotFound,
            null,
            ProcessEligibilityCheckSource.HMRC);

        await _fakeInMemoryDb.SaveChangesAsync();

        var response = await _sut.Exists(dataItem);

        // Assert
        response.Should().BeOfType<EligibilityCheckHash>();
    }

    [Test]
    public async Task Given_HashIsOld_Exists_Should_Return_null()
    {
        // Arrange
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);

        var id = await _sut.Create(
            dataItem,
            CheckEligibilityStatus.parentNotFound,
            null,
            ProcessEligibilityCheckSource.HMRC);

        await _fakeInMemoryDb.SaveChangesAsync();

        var hashItem = _fakeInMemoryDb.EligibilityCheckHashes
            .First(x => x.EligibilityCheckHashID.Equals(id));

        hashItem.TimeStamp = hashItem.TimeStamp.AddDays(-(_hashCheckDays + 1));

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.Exists(dataItem);

        // Assert
        response.Should().BeNull();
    }

    [Test]
    public async Task Given_WorkingFamilies_HashIsOld_Exists_Should_Return_null()
    {
        // Arrange
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var wf = _fixture.Create<CheckEligibilityRequestData>();
        wf.DateOfBirth = "1990-01-01";
        wf.Type = CheckEligibilityType.WorkingFamilies;
        var dataItem = GetCheckProcessData(wf);

        var id = await _sut.Create(
            dataItem,
            CheckEligibilityStatus.parentNotFound,
            null,
            ProcessEligibilityCheckSource.HMRC);

        await _fakeInMemoryDb.SaveChangesAsync();

        var hashItem = _fakeInMemoryDb.EligibilityCheckHashes
            .First(x => x.EligibilityCheckHashID.Equals(id));

        hashItem.TimeStamp = hashItem.TimeStamp.AddDays(-(_hashCheckDaysWF + 1));

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.Exists(dataItem);

        // Assert
        response.Should().BeNull();
    }

    // --- ExistsBatch: batched replacement for Exists() used by bulk checks (ELIG-3354 / see
    // docs/bulk-check-hash-batching-fix.md). These tests mirror the single-record Exists() tests
    // above, but exercise the whole-batch, one-query behaviour instead.

    [Test]
    public async Task Given_MultipleItems_ExistsBatch_Should_Return_MatchesKeyedByHash()
    {
        // Arrange - two records with existing, still-valid hashes, and one with no hash at all.
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");

        var matched1 = GetCheckProcessData(_fixture.Create<CheckEligibilityRequestData>());
        matched1.DateOfBirth = "1990-01-01";
        var matched2 = GetCheckProcessData(_fixture.Create<CheckEligibilityRequestData>());
        matched2.DateOfBirth = "1991-02-02";
        var unmatched = GetCheckProcessData(_fixture.Create<CheckEligibilityRequestData>());
        unmatched.DateOfBirth = "1992-03-03";

        await _sut.Create(matched1, CheckEligibilityStatus.eligible, null, ProcessEligibilityCheckSource.HMRC);
        await _sut.Create(matched2, CheckEligibilityStatus.notEligible, null, ProcessEligibilityCheckSource.HMRC);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act - ONE call covering all three records, instead of three separate Exists() calls.
        var response = await _sut.ExistsBatch(new[] { matched1, matched2, unmatched }, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        response.Should().HaveCount(2);
        response.Should().ContainKey(matched1.GetHash());
        response.Should().ContainKey(matched2.GetHash());
        response.Should().NotContainKey(unmatched.GetHash());
        response[matched1.GetHash()].Outcome.Should().Be(CheckEligibilityStatus.eligible);
        response[matched2.GetHash()].Outcome.Should().Be(CheckEligibilityStatus.notEligible);
    }

    [Test]
    public async Task Given_DuplicateHashesInBatch_ExistsBatch_Should_Return_OneEntryPerHash()
    {
        // Arrange - two IDENTICAL records (e.g. a duplicate submitted twice in the same bulk file)
        // must not cause a duplicate-key exception when building the result dictionary.
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1990-01-01";
        var item = GetCheckProcessData(request);
        var duplicateItem = GetCheckProcessData(request);

        await _sut.Create(item, CheckEligibilityStatus.eligible, null, ProcessEligibilityCheckSource.HMRC);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _sut.ExistsBatch(new[] { item, duplicateItem }, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        await act.Should().NotThrowAsync();
        var response = await _sut.ExistsBatch(new[] { item, duplicateItem }, CheckEligibilityType.FreeSchoolMeals);
        response.Should().HaveCount(1);
        response.Should().ContainKey(item.GetHash());
    }

    [Test]
    public async Task Given_EmptyBatch_ExistsBatch_Should_Return_EmptyDictionary()
    {
        // Act
        var response = await _sut.ExistsBatch(Enumerable.Empty<CheckProcessData>(), CheckEligibilityType.FreeSchoolMeals);

        // Assert
        response.Should().BeEmpty();
    }

    [Test]
    public async Task Given_HashIsOld_ExistsBatch_Should_Not_Include_It()
    {
        // Arrange - mirrors Given_HashIsOld_Exists_Should_Return_null, but for the batched lookup.
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);

        var id = await _sut.Create(
            dataItem,
            CheckEligibilityStatus.parentNotFound,
            null,
            ProcessEligibilityCheckSource.HMRC);

        await _fakeInMemoryDb.SaveChangesAsync();

        var hashItem = _fakeInMemoryDb.EligibilityCheckHashes
            .First(x => x.EligibilityCheckHashID.Equals(id));
        hashItem.TimeStamp = hashItem.TimeStamp.AddDays(-(_hashCheckDays + 1));
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.ExistsBatch(new[] { dataItem }, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        response.Should().BeEmpty();
    }

    [Test]
    public async Task Given_WorkingFamiliesType_ExistsBatch_Should_UseWFValidityWindow()
    {
        // Arrange - a hash that's within the (longer/shorter, whichever configured) FSM window but
        // OUTSIDE the WorkingFamilies-specific window must be excluded when type=WorkingFamilies.
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");
        var wf = _fixture.Create<CheckEligibilityRequestData>();
        wf.DateOfBirth = "1990-01-01";
        wf.Type = CheckEligibilityType.WorkingFamilies;
        var dataItem = GetCheckProcessData(wf);

        var id = await _sut.Create(
            dataItem,
            CheckEligibilityStatus.eligible,
            null,
            ProcessEligibilityCheckSource.HMRC);

        await _fakeInMemoryDb.SaveChangesAsync();

        var hashItem = _fakeInMemoryDb.EligibilityCheckHashes
            .First(x => x.EligibilityCheckHashID.Equals(id));
        hashItem.TimeStamp = hashItem.TimeStamp.AddDays(-(_hashCheckDaysWF + 1));
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.ExistsBatch(new[] { dataItem }, CheckEligibilityType.WorkingFamilies);

        // Assert
        response.Should().BeEmpty();
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
}