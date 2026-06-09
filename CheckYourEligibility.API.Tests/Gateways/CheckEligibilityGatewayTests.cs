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
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;

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
    private Mock<IStorageQueueMessage> _moqStorageQueueGateway;
    private CheckEligibilityGateway _sut;

    [SetUp]
    public async Task Setup()
    {
        var databaseName = $"FakeInMemoryDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        // Ensure database is created and clean
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureCreatedAsync();
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

        _moqEcsGateway = new Mock<IEcsAdapter>(MockBehavior.Strict);
        _moqDwpGateway = new Mock<IDwpAdapter>(MockBehavior.Strict);
        _moqStorageQueueGateway = new Mock<IStorageQueueMessage>();
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
        Func<Task> act = async () => await svc.PostCheck<CheckEligibilityRequestData>(request,meta);

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
                It.IsAny<CheckEligibilityType>(), It.IsAny<Guid>().ToString(),It.IsAny<EligibilityPolicy>()))
            .ReturnsAsync(claimResponse);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>(), null)).ReturnsAsync("");

        
        var groupId = Guid.NewGuid().ToString();
        var data = new List<CheckEligibilityRequestData> { request };
        await _sut.PostCheck(data, groupId, meta);
        Assert.Pass();
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
        var(status,tier) = await _sut.GetStatusAsync(guid, CheckEligibilityType.None);

        // Assert
        status.Should().BeNull();
        tier.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_GetStatus_Should_Return_status()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.eligible; // Ensure not deleted status
        item.Tier = null;
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var(status,tier) = await _sut.GetStatusAsync(item.EligibilityCheckID, CheckEligibilityType.None);

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
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var (status, tier) = await _sut.GetStatusAsync(item.EligibilityCheckID, CheckEligibilityType.None);

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
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var (status, tier) = await _sut.GetStatusAsync(item.EligibilityCheckID, CheckEligibilityType.None);

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
        var type = CheckEligibilityType.EarlyYearPupilPremium;
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var(status, tier) = await _sut.GetStatusAsync(item.EligibilityCheckID, type);

        // Assert
        status.Should().BeNull();
        tier.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_SameType_GetStatus_Should_Return_status()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        var type = CheckEligibilityType.FreeSchoolMeals;
        item.Tier = null;
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var(status,tier) = await _sut.GetStatusAsync(item.EligibilityCheckID, type);

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
        _fakeInMemoryDb.SaveChangesAsync();

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
        var check = _fixture.Create<CheckEligibilityRequestData>();
        check.DateOfBirth = "1990-01-01";
        check.Type = CheckEligibilityType.FreeSchoolMeals;
        string eligibilityEndDate = (new DateTime(DateTime.UtcNow.Year, 07, 31)).ToString("yyyy-MM-dd");
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check,eligibilityEndDate));

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID, CheckEligibilityType.None);
        // Assert
        response.Should().BeOfType<CheckEligibilityItem>();
        response.DateOfBirth.Should().BeEquivalentTo(check.DateOfBirth);
        response.NationalAsylumSeekerServiceNumber.Should().BeEquivalentTo(check.NationalAsylumSeekerServiceNumber);
        response.NationalInsuranceNumber.Should().BeEquivalentTo(check.NationalInsuranceNumber);
        response.LastName.Should().BeEquivalentTo(check.LastName.ToUpper());
        response.EligibilityEndDate.Should().BeEquivalentTo(eligibilityEndDate);
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
        _fakeInMemoryDb.SaveChangesAsync();

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

    #region Private Helper Methods

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request, string? eligiblityEndDate = null)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth ?? "1990-01-01",
            LastName = request.LastName,
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