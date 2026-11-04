using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Helpers;
using CheckYourEligibility.API.Services;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CheckYourEligibility.API.Tests.Gateways;

[TestFixture]
public class BulkReferencePersistenceTests
{
    private EligibilityCheckContext _db = null!;
    private HashGateway _hashGateway = null!;
    private CheckEligibilityGateway _gateway = null!;
    private EligibilityCheckDataResponseMapper _responseMapper = null!;

    [SetUp]
    public void SetUp()
    {
        var options = TestDbOptions.Create();
        _db = new EligibilityCheckContext(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["HashCheckDays"] = "7",
                ["HashCheckDaysWF"] = "7"
            }).Build();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        _hashGateway = new HashGateway(NullLoggerFactory.Instance, _db, configuration, Mock.Of<IAudit>());
        _gateway = new CheckEligibilityGateway(
            NullLoggerFactory.Instance, _db, mapper, configuration, _hashGateway, Mock.Of<IStorageQueue>());
        _responseMapper = new EligibilityCheckDataResponseMapper(
            NullLogger<EligibilityCheckDataResponseMapper>.Instance, _gateway);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Test]
    public async Task NewChecks_PreserveEachReferenceThroughStorageAndBulkResults(
        [Values(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityType.TwoYearOffer,
            CheckEligibilityType.EarlyYearPupilPremium, CheckEligibilityType.WorkingFamilies)] CheckEligibilityType type,
        [Values(null, "", "  MiXeD  ref, \"A\" / 001  ")] string? reference)
    {
        var first = await _gateway.MapCheck(CreateRequest(type, reference, 1), new CheckMetaData());
        var second = await _gateway.MapCheck(CreateRequest(type, "second-reference", 2), new CheckMetaData());
        first.Status.Should().Be(CheckEligibilityStatus.queuedForProcessing);
        second.Status.Should().Be(CheckEligibilityStatus.queuedForProcessing);
        first.BulkCheckID = second.BulkCheckID = "batch-1";

        // BulkInsert uses SQL-specific behavior; test its mapped CheckData payload
        // through EF persistence here. SQL/queue integration remains a QA check.
        _db.CheckEligibilities.AddRange(first, second);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var persisted = await _db.CheckEligibilities.AsNoTracking().ToListAsync();
        var firstStored = persisted.Single(x => x.EligibilityCheckID == first.EligibilityCheckID);
        var secondStored = persisted.Single(x => x.EligibilityCheckID == second.EligibilityCheckID);
        JObject.Parse(firstStored.CheckData).Value<string>("Reference").Should().Be(reference?.Trim());

        var firstProcessed = MapCheckDataHelper.MapCheckDataBasedOnType(type, firstStored.CheckData);
        var secondProcessed = MapCheckDataHelper.MapCheckDataBasedOnType(type, secondStored.CheckData);
        firstProcessed.Reference.Should().Be(reference?.Trim());
        secondProcessed.Reference.Should().Be("second-reference");
        firstProcessed.GetHash().Should().Be(secondProcessed.GetHash(),
            "Reference and row order must not change the eligibility hash");

        // The processing model must also retain the field when written back as JSON.
        firstStored.CheckData = JsonConvert.SerializeObject(firstProcessed);
        secondStored.CheckData = JsonConvert.SerializeObject(secondProcessed);
        var bulkGateway = new Mock<IBulkCheck>();
        bulkGateway.Setup(x => x.GetBulkCheck("batch-1"))
            .ReturnsAsync(new BulkCheck { BulkCheckID = "batch-1" });
        bulkGateway.Setup(x => x.GetBulkCheckResults("batch-1"))
            .ReturnsAsync(new List<EligibilityCheck> { secondStored, firstStored });
        var resultsUseCase = new GetBulkUploadResultsUseCase(
            bulkGateway.Object, Mock.Of<IAudit>(), _responseMapper,
            NullLogger<GetBulkUploadResultsUseCase>.Instance);

        var results = (await resultsUseCase.Execute("batch-1", new List<int> { 0 })).Data.ToList();

        results.Should().HaveCount(2);
        var firstResult = results.Single(x => x.Order == 1);
        var secondResult = results.Single(x => x.Order == 2);
        firstResult.Reference.Should().Be(reference?.Trim());
        firstResult.ClientIdentifier.Should().Be("row-1");
        firstResult.EligibilityCheckID.Should().Be(first.EligibilityCheckID);
        secondResult.Reference.Should().Be("second-reference");
        secondResult.ClientIdentifier.Should().Be("row-2");
        secondResult.EligibilityCheckID.Should().Be(second.EligibilityCheckID);
        var responseJson = JObject.FromObject(secondResult);
        responseJson.Value<string>("Reference").Should().Be("second-reference");
    }

    [Test]
    public async Task CachedChecks_UseCurrentReferenceIncludingBlank_WithoutChangingCachedRecord(
        [Values(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityType.TwoYearOffer,
            CheckEligibilityType.EarlyYearPupilPremium, CheckEligibilityType.WorkingFamilies)] CheckEligibilityType type,
        [Values(CheckEligibilityStatus.eligible, CheckEligibilityStatus.notEligible)] CheckEligibilityStatus status,
        [Values(null, "", "  current-reference  ")] string? reference)
    {
        var cachedData = MapCheckDataHelper.MapCheckDataBasedOnType(
            type, JsonConvert.SerializeObject(CreateRequest(type, "cached-reference", 99)));
        var tier = type == CheckEligibilityType.FreeSchoolMeals && status == CheckEligibilityStatus.eligible
            ? EligibilityTier.targeted : (EligibilityTier?)null;
        // Hash the original eligibility inputs before enriching the cached result with dates.
        var hashId = await _hashGateway.Create(cachedData, status, tier, ProcessEligibilityCheckSource.HMRC);
        cachedData.EligibilityEndDate = "2027-07-31";
        cachedData.ValidityStartDate = "2026-09-01";
        cachedData.DiscretionaryValidityStartDate = "2026-09-02";
        cachedData.ValidityEndDate = "2026-12-01";
        cachedData.GracePeriodEndDate = "2027-03-31";
        cachedData.EmailAddress = "old@example.com";
        var cached = new EligibilityCheck
        {
            EligibilityCheckID = "cached-check",
            EligibilityCheckHashID = hashId,
            Type = type,
            Status = status,
            Tier = tier,
            Created = DateTime.UtcNow.AddMinutes(-1),
            CheckData = JsonConvert.SerializeObject(cachedData)
        };
        _db.CheckEligibilities.Add(cached);
        await _db.SaveChangesAsync();
        var originalCachedJson = cached.CheckData;

        var current = await _gateway.MapCheck(CreateRequest(type, reference, 1), new CheckMetaData());
        _db.CheckEligibilities.Add(current);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var stored = await _db.CheckEligibilities.AsNoTracking()
            .SingleAsync(x => x.EligibilityCheckID == current.EligibilityCheckID);
        var storedData = JsonConvert.DeserializeObject<CheckProcessData>(stored.CheckData)!;
        var response = _responseMapper.MapCheckDataToResponse(stored);

        stored.Status.Should().Be(status);
        stored.Tier.Should().Be(tier);
        stored.EligibilityCheckHashID.Should().Be(hashId, "the test must exercise a cache hit");
        storedData.Reference.Should().Be(reference?.Trim());
        storedData.Order.Should().Be(1);
        storedData.ClientIdentifier.Should().Be("row-1");
        response.Reference.Should().Be(reference?.Trim());
        if (type == CheckEligibilityType.FreeSchoolMeals)
        {
            storedData.EligibilityEndDate.Should().Be("2027-07-31");
            storedData.EmailAddress.Should().Be("current@example.com");
        }
        if (type == CheckEligibilityType.WorkingFamilies)
        {
            storedData.ValidityStartDate.Should().Be("2026-09-01");
            storedData.DiscretionaryValidityStartDate.Should().Be("2026-09-02");
            storedData.ValidityEndDate.Should().Be("2026-12-01");
            storedData.GracePeriodEndDate.Should().Be("2027-03-31");
        }
        (await _db.CheckEligibilities.AsNoTracking()
            .SingleAsync(x => x.EligibilityCheckID == "cached-check"))
            .CheckData.Should().Be(originalCachedJson);
    }

    [TestCase(CheckEligibilityType.FreeSchoolMeals)]
    [TestCase(CheckEligibilityType.TwoYearOffer)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium)]
    [TestCase(CheckEligibilityType.WorkingFamilies)]
    public void HistoricalCheckData_WithoutReference_RemainsReadable(CheckEligibilityType type)
    {
        const string historicalJson = "{\"LastName\":\"SMITH\",\"DateOfBirth\":\"1990-01-01\",\"NationalInsuranceNumber\":\"AB123456C\",\"Order\":7}";
        var check = new EligibilityCheck { Type = type, CheckData = historicalJson };

        MapCheckDataHelper.MapCheckDataBasedOnType(type, historicalJson).Reference.Should().BeNull();
        var response = _responseMapper.MapCheckDataToResponse(check);

        response.Reference.Should().BeNull();
        response.Order.Should().Be(7);
        response.NationalInsuranceNumber.Should().Be("AB123456C");
    }

    private static CheckEligibilityRequestDataBase CreateRequest(
        CheckEligibilityType type, string? reference, int order)
    {
        CheckEligibilityRequestDataBase request = type == CheckEligibilityType.WorkingFamilies
            ? new CheckEligibilityRequestWorkingFamiliesBulkData
            {
                EligibilityCode = "50012345678", Order = order, ClientIdentifier = $"row-{order}"
            }
            : new CheckEligibilityRequestBulkData { Order = order, ClientIdentifier = $"row-{order}" };
        request.Type = type;
        request.LastName = "Smith";
        request.DateOfBirth = type == CheckEligibilityType.WorkingFamilies ? "2023-01-01" : "1990-01-01";
        request.NationalInsuranceNumber = "AB123456C";
        request.EmailAddress = "current@example.com";
        request.Reference = reference;
        return request;
    }
}
