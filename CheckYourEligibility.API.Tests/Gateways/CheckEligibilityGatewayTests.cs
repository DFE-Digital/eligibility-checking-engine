using AutoFixture;
using AutoMapper;
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
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace CheckYourEligibility.API.Tests;

public class CheckEligibilityGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private HashGateway _hashGateway;
    private IMapper _mapper;
    private Mock<IAudit> _moqAudit;
    private Mock<IEcsAdapter> _moqEcsGateway;
    private Mock<IDwpAdapter> _moqDwpGateway;
    private Mock<IStorageQueue> _moqStorageQueueGateway;
    private CheckEligibilityGateway _sut;
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(nameof(CheckEligibilityGatewayTests), InMemoryDatabaseRoot)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);
        await _fakeInMemoryDb.Database.EnsureDeletedAsync();
        await _fakeInMemoryDb.Database.EnsureCreatedAsync();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        var configForSmsApi = new Dictionary<string, string>
        {
            ["BulkEligibilityCheckLimit"] = "250",
            ["QueueFsmCheckStandard"] = "notSet",
            ["QueueFsmCheckBulk"] = "notSet",
            ["HashCheckDays"] = "7",
            ["HashCheckDaysWF"] = "1",
            ["Dwp:UseEcsforChecksWF"] = "false"
        };

        var queueConfig = new Dictionary<string, string>
        {
            ["Queue:Bulk:FreeSchoolMeals:Frontend"] = "process-bulk-fsm-frontend-eligibility-queue",
            ["Queue:Bulk:FreeSchoolMeals:Api"] = "process-bulk-fsm-api-eligibility-queue",
            ["Queue:Bulk:WorkingFamilies"] = "process-bulk-wf-eligibility-queue",
            ["Queue:Bulk:TwoYearOffer"] = "process-bulk-eligibility-queue"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .AddInMemoryCollection(queueConfig)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _moqEcsGateway = new Mock<IEcsAdapter>(MockBehavior.Strict);
        _moqDwpGateway = new Mock<IDwpAdapter>(MockBehavior.Strict);
        _moqStorageQueueGateway = new Mock<IStorageQueue>();
        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);
        _hashGateway = new HashGateway(new NullLoggerFactory(), _fakeInMemoryDb, _configuration, _moqAudit.Object);


        _sut = new CheckEligibilityGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper,
            _configuration, _hashGateway, _moqStorageQueueGateway.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task Given_PostCheck_ExceptionRaised()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        var meta = _fixture.Create<CheckMetaData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;

        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);

        var svc = new CheckEligibilityGateway(new NullLoggerFactory(), db.Object, _mapper, _configuration,
            _hashGateway, _moqStorageQueueGateway.Object);
        db.Setup(x => x.CheckEligibilities.AddAsync(It.IsAny<EligibilityCheck>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        // Act
        Func<Task> act = async () => await svc.PostCheck<CheckEligibilityRequestData>(request, meta);

        // Assert
        act.Should().ThrowExactlyAsync<DbUpdateException>();
    }

    [Ignore("Disabled due to using DB in memory")]
    [Test]
    public async Task Given_PostBulk_Should_Complete()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        var claimResponse = _fixture.Create<CAPIClaimResponseBase>();
        var citizenResponse = _fixture.Create<CAPICitizenResponse>();
        var meta = _fixture.Create<CheckMetaData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;
        var key = string.IsNullOrEmpty(request.NationalInsuranceNumber)
            ? request.NationalAsylumSeekerServiceNumber
            : request.NationalInsuranceNumber;
        // Arrange standard policy

        //Set UpValid hmrc check
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = request.NationalInsuranceNumber,
            Surname = request.LastName,
            DateOfBirth = DateTime.Parse(request.DateOfBirth)
        });
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<Guid>().ToString()))
            .ReturnsAsync(citizenResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<Guid>().ToString(), It.IsAny<EligibilityPolicy>()))
            .ReturnsAsync(claimResponse);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");


        var groupId = Guid.NewGuid().ToString();
        var data = new List<CheckEligibilityRequestData> { request };
        await _sut.PostCheck(data, groupId, meta);
        Assert.Pass();
    }

    // --- Fix for ELIG-3354 / bulk-check hash batching + Failed status marking.
    // See docs/bulk-check-hash-batching-fix.md for the full incident these tests cover.

    [Test]
    public async Task Given_PostCheck_Bulk_MappingOrHashingThrows_Should_MarkBulkCheckFailed_AndRethrow()
    {
        // Arrange - simulate a failure DURING the batched mapping/hashing phase (not the final
        // insert). Before this fix, such a failure escaped PostCheck entirely and the BulkCheck
        // was left stuck at InProgress forever with no error ever raised against it.
        var groupId = Guid.NewGuid().ToString();
        var meta = _fixture.Create<CheckMetaData>();

        _fakeInMemoryDb.BulkChecks.Add(new CheckYourEligibility.API.Domain.BulkCheck
        {
            BulkCheckID = groupId,
            Status = BulkCheckStatus.InProgress,
            SubmittedDate = DateTime.UtcNow,
            NumberOfRecords = 1,
            EligibilityType = CheckEligibilityType.FreeSchoolMeals,
            FinalNameInCheck = "Test",
            Filename = "test.csv",
            SubmittedBy = "tester"
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        var moqHashGateway = new Mock<IHash>(MockBehavior.Strict);
        moqHashGateway
            .Setup(x => x.ExistsBatch(It.IsAny<IEnumerable<CheckProcessData>>(), It.IsAny<CheckEligibilityType>()))
            .ThrowsAsync(new InvalidOperationException("simulated failure mid-batch"));

        var svc = new CheckEligibilityGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper, _configuration,
            moqHashGateway.Object, _moqStorageQueueGateway.Object);

        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;
        var data = new List<CheckEligibilityRequestData> { request };

        // Act
        Func<Task> act = async () => await svc.PostCheck(data, groupId, meta);

        // Assert - the exception still propagates (so the caller's own "Background PostCheck
        // failed" log line still fires)...
        await act.Should().ThrowAsync<InvalidOperationException>();

        // ...but unlike before this fix, the BulkCheck is now left in a terminal Failed state
        // instead of stuck at InProgress forever with zero signal.
        var bulkCheck = await _fakeInMemoryDb.BulkChecks.FirstOrDefaultAsync(x => x.BulkCheckID == groupId);
        bulkCheck.Should().NotBeNull();
        bulkCheck!.Status.Should().Be(BulkCheckStatus.Failed);
        bulkCheck.CompletedDate.Should().NotBeNull();
    }

    [Test]
    public async Task Given_NoHashMatch_MapChecksBulk_Should_Leave_Status_QueuedForProcessing()
    {
        // Arrange - nothing seeded in EligibilityCheckHashes, so the batched lookup finds nothing.
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1985-05-05";
        var meta = _fixture.Create<CheckMetaData>();

        // Act
        var mapped = await _sut.MapChecksBulk(new List<IEligibilityServiceType> { request }, meta);

        // Assert
        mapped.Should().HaveCount(1);
        mapped[0].Status.Should().Be(CheckEligibilityStatus.queuedForProcessing);
        mapped[0].EligibilityCheckHashID.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task Given_WorkingFamiliesHashMatch_MapChecksBulk_Should_CopyPriorCheckData()
    {
        // Arrange - seed a hash + a prior CheckEligibilities row it resolves to, then submit a
        // NEW record that hashes identically (same NINO/LastName/DateOfBirth/WF fields) but with
        // its own ClientIdentifier - proving the batched lookup (ExistsBatch + the batched retry
        // loop in ApplyPriorCheckDataBatch) resolves and copies data correctly, not just for a
        // single record processed alone.
        var priorRequest = _fixture.Create<CheckEligibilityRequestWorkingFamiliesBulkData>();
        priorRequest.DateOfBirth = "1990-01-01";
        priorRequest.Type = CheckEligibilityType.WorkingFamilies;
        priorRequest.EligibilityCode = "PRIOR-CODE";

        var newRequest = _fixture.Create<CheckEligibilityRequestWorkingFamiliesBulkData>();
        newRequest.DateOfBirth = priorRequest.DateOfBirth;
        newRequest.LastName = priorRequest.LastName;
        newRequest.NationalInsuranceNumber = priorRequest.NationalInsuranceNumber;
        newRequest.Type = CheckEligibilityType.WorkingFamilies;
        newRequest.EligibilityCode = priorRequest.EligibilityCode;
        newRequest.GracePeriodEndDate = priorRequest.GracePeriodEndDate;
        newRequest.ValidityStartDate = priorRequest.ValidityStartDate;
        newRequest.ValidityEndDate = priorRequest.ValidityEndDate;
        newRequest.ClientIdentifier = "NEW-CLIENT-ID";

        var hashSource = new CheckProcessData
        {
            DateOfBirth = priorRequest.DateOfBirth,
            LastName = priorRequest.LastName,
            NationalInsuranceNumber = priorRequest.NationalInsuranceNumber,
            Type = CheckEligibilityType.WorkingFamilies,
            EligibilityCode = priorRequest.EligibilityCode,
            GracePeriodEndDate = priorRequest.GracePeriodEndDate,
            ValidityStartDate = priorRequest.ValidityStartDate,
            ValidityEndDate = priorRequest.ValidityEndDate
        };
        var hashId = await _hashGateway.Create(hashSource, CheckEligibilityStatus.eligible, null, ProcessEligibilityCheckSource.HMRC);
        await _fakeInMemoryDb.SaveChangesAsync();

        _fakeInMemoryDb.CheckEligibilities.Add(new EligibilityCheck
        {
            EligibilityCheckID = Guid.NewGuid().ToString(),
            EligibilityCheckHashID = hashId,
            Status = CheckEligibilityStatus.eligible,
            Type = CheckEligibilityType.WorkingFamilies,
            Created = DateTime.UtcNow.AddDays(-1),
            Updated = DateTime.UtcNow.AddDays(-1),
            CheckData = JsonConvert.SerializeObject(hashSource)
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        var meta = _fixture.Create<CheckMetaData>();

        // Act
        var mapped = await _sut.MapChecksBulk(new List<IEligibilityServiceType> { newRequest }, meta);

        // Assert
        mapped.Should().HaveCount(1);
        var result = mapped[0];
        result.Status.Should().Be(CheckEligibilityStatus.eligible);
        result.EligibilityCheckHashID.Should().Be(hashId);

        var copiedData = JsonConvert.DeserializeObject<CheckProcessData>(result.CheckData);
        copiedData!.ClientIdentifier.Should().Be("NEW-CLIENT-ID"); // overwritten from the NEW submission
        copiedData.EligibilityCode.Should().Be("PRIOR-CODE"); // copied across from the PRIOR check, unmodified
    }

    [Test]
    public async Task Given_FreeSchoolMealsHashMatch_MapChecksBulk_Should_CopyPriorCheckData_SingleAttempt()
    {
        // Arrange - same idea as the WorkingFamilies test above, but for the FreeSchoolMeals
        // branch, which only ever does a single one-shot lookup (no retry).
        var priorRequest = _fixture.Create<CheckEligibilityRequestBulkData>();
        priorRequest.DateOfBirth = "1988-08-08";
        priorRequest.Type = CheckEligibilityType.FreeSchoolMeals;

        var newRequest = _fixture.Create<CheckEligibilityRequestBulkData>();
        newRequest.DateOfBirth = priorRequest.DateOfBirth;
        newRequest.LastName = priorRequest.LastName;
        newRequest.NationalInsuranceNumber = priorRequest.NationalInsuranceNumber;
        newRequest.NationalAsylumSeekerServiceNumber = priorRequest.NationalAsylumSeekerServiceNumber;
        newRequest.Type = CheckEligibilityType.FreeSchoolMeals;
        newRequest.ClientIdentifier = "NEW-CLIENT-ID";

        var hashSource = new CheckProcessData
        {
            DateOfBirth = priorRequest.DateOfBirth,
            LastName = priorRequest.LastName,
            NationalInsuranceNumber = priorRequest.NationalInsuranceNumber,
            NationalAsylumSeekerServiceNumber = priorRequest.NationalAsylumSeekerServiceNumber,
            Type = CheckEligibilityType.FreeSchoolMeals,
            EligibilityEndDate = "2030-01-01"
        };
        var hashId = await _hashGateway.Create(hashSource, CheckEligibilityStatus.notEligible, null, ProcessEligibilityCheckSource.HMRC);
        await _fakeInMemoryDb.SaveChangesAsync();

        _fakeInMemoryDb.CheckEligibilities.Add(new EligibilityCheck
        {
            EligibilityCheckID = Guid.NewGuid().ToString(),
            EligibilityCheckHashID = hashId,
            Status = CheckEligibilityStatus.notEligible,
            Type = CheckEligibilityType.FreeSchoolMeals,
            Created = DateTime.UtcNow.AddDays(-1),
            Updated = DateTime.UtcNow.AddDays(-1),
            CheckData = JsonConvert.SerializeObject(hashSource)
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        var meta = _fixture.Create<CheckMetaData>();

        // Act
        var mapped = await _sut.MapChecksBulk(new List<IEligibilityServiceType> { newRequest }, meta);

        // Assert
        mapped.Should().HaveCount(1);
        mapped[0].Status.Should().Be(CheckEligibilityStatus.notEligible);

        var copiedData = JsonConvert.DeserializeObject<CheckProcessData>(mapped[0].CheckData);
        copiedData!.ClientIdentifier.Should().Be("NEW-CLIENT-ID");
        copiedData.EligibilityEndDate.Should().Be("2030-01-01"); // preserved from the prior check
    }


    [Test]
    public void Given_validRequest_PostFeature_Should_Return_id()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        var meta = _fixture.Create<CheckMetaData>();
        request.DateOfBirth = "1970-02-01";

        // Act
        var response = _sut.PostCheck(request, meta);

        // Assert
        response.Result.Id.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Given_InValidRequest_GetStatus_Should_Return_null()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();

        // Act
        var (status, tier, _) = await _sut.GetStatusAsync(
            guid,
            CheckEligibilityType.None);

        // Assert
        status.Should().BeNull();
        tier.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_GetStatus_Should_Return_status()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.eligible;
        item.Tier = null;
        item.IsDeleted = false;
        item.CheckData = "{}";

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetStatusAsync(
            item.EligibilityCheckID,
            CheckEligibilityType.None);

        var status = result.Item1;
        var tier = result.Item2;

        // Assert
        status.ToString().Should().Be(item.Status.ToString());
        tier.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_GetStatus_Should_Return_Eligible_Expanded()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.eligible; // Ensure not deleted status
        item.Tier = EligibilityTier.expanded;
        item.IsDeleted = false;
        item.CheckData = "{}";

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetStatusAsync(
            item.EligibilityCheckID,
            CheckEligibilityType.None);

        var status = result.Item1;
        var tier = result.Item2;
        var errorCode = result.Item3;

        // Assert
        status.ToString().Should().Be(item.Status.ToString());
        tier.ToString().Should().Be(EligibilityTier.expanded.ToString());
    }

    [Test]
    public async Task Given_ValidRequest_GetStatus_Should_Return_Eligible_Targeted()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.eligible; // Ensure not deleted status
        item.Tier = EligibilityTier.targeted;
        item.CheckData = "{}";

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetStatusAsync(
            item.EligibilityCheckID,
            CheckEligibilityType.None);

        var status = result.Item1;
        var tier = result.Item2;        

        // Assert
        status.ToString().Should().Be(item.Status.ToString());
        tier.ToString().Should().Be(EligibilityTier.targeted.ToString());
    }

    [Test]
    public async Task Given_ValidRequest_DiffType_GetStatus_Should_Return_null()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = "{}";

        var type = CheckEligibilityType.EarlyYearPupilPremium;

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetStatusAsync(
            item.EligibilityCheckID,
            type);

        var status = result.Item1;
        var tier = result.Item2;

        // Assert
        status.Should().BeNull();
        tier.Should().BeNull();
    }

    [Test]
    public async Task Given_CheckDataContainsErrorCode_GetStatus_Should_Return_ErrorCode()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.Status = CheckEligibilityStatus.eligible;
        item.Tier = null;
        item.IsDeleted = false;
        item.CheckData = JsonConvert.SerializeObject(new CheckProcessData
        {
            ErrorCode = "STE10"
        });

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetStatusAsync(
            item.EligibilityCheckID,
            CheckEligibilityType.FreeSchoolMeals);

        var status = result.Item1;
        var tier = result.Item2;
        var errorCode = result.Item3;

        // Assert
        status.Should().Be(item.Status);
        tier.Should().BeNull();
        errorCode.Should().Be("STE10");
    }

    [Test]
    public async Task Given_ValidRequest_SameType_GetStatus_Should_Return_status()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.Tier = null;
        item.CheckData = "{}";

        var type = CheckEligibilityType.FreeSchoolMeals;

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetStatusAsync(
            item.EligibilityCheckID,
            type);

        var status = result.Item1;
        var tier = result.Item2;

        // Assert
        status.ToString().Should().Be(item.Status.ToString());
        tier.Should().BeNull();
    }

    [Test]
    public void Given_InValidRequest_GetItem_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.GetItem<CheckEligibilityItem>(request, CheckEligibilityType.None);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_DiffType_GetItem_Should_Return_null()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        var type = CheckEligibilityType.TwoYearOffer;
        var check = _fixture.Create<CheckEligibilityRequestData>();
        check.DateOfBirth = "1990-01-01";
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check));

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID, type);
        // Assert
        response.Should().BeNull();
    }

    [Test]
    public async Task Given_FSM_ValidRequest_GetItem_Should_Return_Item()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.Status = CheckEligibilityStatus.eligible;
        item.IsDeleted = false;
        var check = _fixture.Create<CheckEligibilityRequestData>();
        check.DateOfBirth = "1990-01-01";
        check.Type = CheckEligibilityType.FreeSchoolMeals;
        check.FirstName = "Alex";
        check.ChildFirstName = "Sam";
        check.ChildLastName = "Tester";
        check.ChildDateOfBirth = "2016-04-12";
        check.ChildSchoolURN = "123456";
        string eligibilityEndDate = (new DateTime(DateTime.UtcNow.Year, 07, 31)).ToString("yyyy-MM-dd");
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check, eligibilityEndDate));

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID, CheckEligibilityType.None);
        // Assert
        response.Should().BeOfType<CheckEligibilityItem>();
        response.DateOfBirth.Should().BeEquivalentTo(check.DateOfBirth);
        response.NationalAsylumSeekerServiceNumber.Should().BeEquivalentTo(check.NationalAsylumSeekerServiceNumber);
        response.NationalInsuranceNumber.Should().BeEquivalentTo(check.NationalInsuranceNumber);
        response.LastName.Should().BeEquivalentTo(check.LastName.ToUpper());
        response.EligibilityEndDate.Should().BeEquivalentTo(eligibilityEndDate);
        response.FirstName.Should().BeEquivalentTo(check.FirstName);
        response.ChildFirstName.Should().BeEquivalentTo(check.ChildFirstName);
        response.ChildLastName.Should().BeEquivalentTo(check.ChildLastName);
        response.ChildDateOfBirth.Should().BeEquivalentTo(check.ChildDateOfBirth);
        response.ChildSchoolURN.Should().BeEquivalentTo(check.ChildSchoolURN);
    }

    [Test]
    public async Task Given_ValidRequest_SameType_GetItem_Should_Return_Item()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var type = CheckEligibilityType.FreeSchoolMeals; // Use FSM instead of random type
        item.Type = type;
        item.Status = CheckEligibilityStatus.queuedForProcessing;// ensure it is not a 'deleted' status.
        // Set navigation properties to null to avoid creating additional entities
        item.EligibilityCheckHash = null;
        item.EligibilityCheckHashID = null;
        item.BulkCheck = null;

        var check = _fixture.Create<CheckEligibilityRequestData>();
        check.DateOfBirth = "1990-01-01";
        check.Type = type; // Ensure both have the same type
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check));

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID, type);
        // Assert
        response.Should().BeOfType<CheckEligibilityItem>();
        response.DateOfBirth.Should().BeEquivalentTo(check.DateOfBirth);
        response.NationalAsylumSeekerServiceNumber.Should().BeEquivalentTo(check.NationalAsylumSeekerServiceNumber);
        response.NationalInsuranceNumber.Should().BeEquivalentTo(check.NationalInsuranceNumber);
        response.LastName.Should().BeEquivalentTo(check.LastName.ToUpper());
    }

    [Test]
    public async Task Given_ValidRequest_GetItem_Should_Return_Working_Families_Item()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.WorkingFamilies;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var check = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        check.LastName = "simpson";
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check));
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID, CheckEligibilityType.None);
        // Assert
        response.Should().BeOfType<CheckEligibilityItem>();
        response.EligibilityCode.Should().BeEquivalentTo(check.EligibilityCode);
        response.ValidityStartDate.Should().BeEquivalentTo(check.ValidityStartDate);
        response.ValidityEndDate.Should().BeEquivalentTo(check.ValidityEndDate);
        response.GracePeriodEndDate.Should().BeEquivalentTo(check.GracePeriodEndDate);
        response.LastName.Should().BeEquivalentTo(check.LastName.ToUpper());
        response.NationalInsuranceNumber.Should().BeEquivalentTo(check.NationalInsuranceNumber);
        response.DateOfBirth.Should().BeEquivalentTo(check.DateOfBirth);
    }

    [Test]
    public void Given_InValidRequest_UpdateEligibilityCheckStatus_Should_Return_null()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();

        // Act
        var response = _sut.UpdateEligibilityCheckStatus(guid, request.Data);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_UpdateEligibilityCheckStatus_Should_Return_UpdatedStatus()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        var requestUpdateStatus = _fixture.Create<EligibilityCheckStatusData>();

        // Act
        var statusUpdate = await _sut.UpdateEligibilityCheckStatus(item.EligibilityCheckID, requestUpdateStatus);

        // Assert
        statusUpdate.Should().BeOfType<CheckEligibilityStatusResponse>();
        statusUpdate.Data.Status.Should().BeEquivalentTo(requestUpdateStatus.Status.ToString());
    }


    [Test]
    public async Task Given_InvalidRequest_DeleteBulkEligibilityChecks_Should_Return_ErrorMessage()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        var requestUpdateStatus = _fixture.Create<EligibilityCheckStatusData>();

        // Act
        Func<Task> act = async () => await _sut.DeleteByBulkCheckId(string.Empty);

        // Assert

        act.Should().ThrowExactlyAsync<ValidationException>();
    }


    [Test]
    public async Task Given_ValidRequest_DeleteBulkEligibilityChecks_With5Records_Should_Delete5Records()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();

        for (var i = 0; i < 5; i++)
        {
            var item = _fixture.Create<EligibilityCheck>();
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.BulkCheckID = groupId;
            item.Status = CheckEligibilityStatus.eligible; // Ensure not already deleted
            item.IsDeleted = false;
            // Set navigation properties to null to avoid creating additional entities
            item.EligibilityCheckHash = null;
            item.EligibilityCheckHashID = null;
            item.BulkCheck = null;
            _fakeInMemoryDb.CheckEligibilities.Add(item);
        }

        var item2 = _fixture.Create<EligibilityCheck>();
        item2.EligibilityCheckID = Guid.NewGuid().ToString();
        // Different group to ensure it's not deleted
        item2.BulkCheckID = Guid.NewGuid().ToString();
        item2.Status = CheckEligibilityStatus.eligible; // Ensure not already deleted
        // Set navigation properties to null to avoid creating additional entities
        item2.EligibilityCheckHash = null;
        item2.EligibilityCheckHashID = null;
        item2.BulkCheck = null;
        _fakeInMemoryDb.CheckEligibilities.Add(item2);

        await _fakeInMemoryDb.SaveChangesAsync();

        // Verify records were actually saved
        var savedCount = await _fakeInMemoryDb.CheckEligibilities.CountAsync(x => x.BulkCheckID == groupId && x.IsDeleted == false);
        savedCount.Should().Be(5, "All 5 records should be saved before deletion");

        var requestUpdateStatus = _fixture.Create<EligibilityCheckStatusData>();

        // Act
        var deleteRespomse = await _sut.DeleteByBulkCheckId(groupId);

        // Assert
        //deleteRespomse.Should().BeOfType<CheckEligibilityBulkDeleteResponse>();
        //deleteRespomse.DeletedCount.Should().Be(5);
        deleteRespomse.Status.Should().BeEquivalentTo("Success");
    }

    [Test]
    public async Task Given_ValidRequest_DeleteBulkEligibilityChecks_Should_Set_Status_Deleted()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();

        for (var i = 0; i < 5; i++)
        {
            var item = _fixture.Create<EligibilityCheck>();
            item.EligibilityCheckID = Guid.NewGuid().ToString();
            item.BulkCheckID = groupId;
            item.Status = CheckEligibilityStatus.eligible; // Ensure not already deleted
            // Set navigation properties to null to avoid creating additional entities
            item.EligibilityCheckHash = null;
            item.EligibilityCheckHashID = null;
            item.BulkCheck = null;
            _fakeInMemoryDb.CheckEligibilities.Add(item);
        }

        await _fakeInMemoryDb.SaveChangesAsync();

        var requestUpdateStatus = _fixture.Create<EligibilityCheckStatusData>();

        // Act
        var deleteResponse = await _sut.DeleteByBulkCheckId(groupId);

        // Assert
        var deletedRecords = await _fakeInMemoryDb.CheckEligibilities.Where(x => x.BulkCheckID == groupId).ToListAsync();
        deletedRecords.Should().NotBeEmpty("There should be records with the specified BulkCheckID");
        deletedRecords.All(x => x.IsDeleted).Should().BeTrue("All records with the specified BulkCheckID should be marked as deleted");
    }


    [Test]
    public async Task Given_ValidRequest_DeleteBulkEligibilityChecks_With0Records_Should_Delete0Records()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();

        for (var i = 0; i < 5; i++)
        {
            var item = _fixture.Create<EligibilityCheck>();
            item.BulkCheckID = groupId;
            _fakeInMemoryDb.CheckEligibilities.Add(item);
            await _fakeInMemoryDb.SaveChangesAsync();
        }

        var item2 = _fixture.Create<EligibilityCheck>();
        _fakeInMemoryDb.CheckEligibilities.Add(item2);

        await _fakeInMemoryDb.SaveChangesAsync();

        var requestUpdateStatus = _fixture.Create<EligibilityCheckStatusData>();

        // Act
        Func<Task> act = async () => await _sut.DeleteByBulkCheckId(Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactlyAsync<ValidationException>();
    }

    [TestCase(CheckEligibilityType.FreeSchoolMeals,"api-user", "process-bulk-fsm-api-eligibility-queue")]
    [TestCase(CheckEligibilityType.FreeSchoolMeals,"free-school-meals-admin", "process-bulk-fsm-frontend-eligibility-queue")]
    [TestCase(CheckEligibilityType.TwoYearOffer, "childcare-admin", "process-bulk-eligibility-queue")]
    [TestCase(CheckEligibilityType.WorkingFamilies, "childcare-admin", "process-bulk-wf-eligibility-queue")]
    public void GetBulkQueueName_Should_Return_Correct_QueueName(CheckEligibilityType type, string source, string queueName)
    {

        // Arrange
          var method = _sut.GetType().GetMethod("GetBulkQueueName",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = method!.Invoke(_sut,new object[] { type, source });

        // Assert
        Assert.That(result, Is.EqualTo(queueName));

    }

    #region Private Helper Methods

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request, string? eligiblityEndDate = null)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth ?? "1990-01-01",
            LastName = request.LastName,
            FirstName = request.FirstName,
            ChildFirstName = request.ChildFirstName,
            ChildLastName = request.ChildLastName,
            ChildDateOfBirth = request.ChildDateOfBirth,
            ChildSchoolURN = request.ChildSchoolURN,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = request.Type,
            EligibilityEndDate = eligiblityEndDate
        };
    }
    private Domain.BulkCheck GetBulkCheckWithEligibilityChecks(int numberOfChecks, CheckEligibilityType type, int localAuthorityId)
    {
        var bulkCheck = new Domain.BulkCheck
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
            FirstName = request.FirstName,
            ChildFirstName = request.ChildFirstName,
            ChildLastName = request.ChildLastName,
            ChildDateOfBirth = request.ChildDateOfBirth,
            ChildSchoolURN = request.ChildSchoolURN,
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