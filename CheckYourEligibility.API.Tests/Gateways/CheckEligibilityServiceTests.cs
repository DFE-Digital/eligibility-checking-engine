// Ignore Spelling: Levenshtein

using System.Globalization;
using AutoFixture;
using AutoMapper;
using Azure.Storage.Queues;
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

public class CheckEligibilityServiceTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private HashGateway _hashGateway;
    private IMapper _mapper;
    private Mock<IAudit> _moqAudit;
    private Mock<IDwpGateway> _moqDwpGateway;
    private CheckEligibilityGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        var configForSmsApi = new Dictionary<string, string>
        {
            { "QueueFsmCheckStandard", "notSet" },
            { "QueueFsmCheckBulk", "notSet" },
            { "HashCheckDays", "7" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _moqDwpGateway = new Mock<IDwpGateway>(MockBehavior.Strict);
        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);
        _hashGateway = new HashGateway(new NullLoggerFactory(), _fakeInMemoryDb, _configuration, _moqAudit.Object);


        _sut = new CheckEligibilityGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper,
            new QueueServiceClient(webJobsConnection),
            _configuration, _moqDwpGateway.Object, _moqAudit.Object, _hashGateway);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public async Task Given_PostCheck_ExceptionRaised()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;

        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);

        var svc = new CheckEligibilityGateway(new NullLoggerFactory(), db.Object, _mapper, null, _configuration,
            _moqDwpGateway.Object, _moqAudit.Object, _hashGateway);
        db.Setup(x => x.CheckEligibilities.AddAsync(It.IsAny<EligibilityCheck>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        // Act
        Func<Task> act = async () => await svc.PostCheck<CheckEligibilityRequestData>(request);

        // Assert
        act.Should().ThrowExactlyAsync<DbUpdateException>();
    }

    [Test]
    public async Task Given_PostCheck_HashIsOldSoNewOne_generated()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;

        //Set UpValid hmrc check
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = request.NationalInsuranceNumber,
            Surname = request.LastName,
            DateOfBirth = DateTime.Parse(request.DateOfBirth)
        });
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");
        // Act/Assert
        var response = await _sut.PostCheck(request);


        var baseItem = _fakeInMemoryDb.CheckEligibilities.FirstOrDefault(x => x.EligibilityCheckID == response.Id);
        baseItem.EligibilityCheckHashID.Should().BeNull();
        await _sut.ProcessCheck(response.Id, _fixture.Create<AuditData>());
        baseItem = _fakeInMemoryDb.CheckEligibilities.Include(x => x.EligibilityCheckHash)
            .FirstOrDefault(x => x.EligibilityCheckID == response.Id);
        baseItem.EligibilityCheckHash.Should().NotBeNull();
        var BaseHash =
            _fakeInMemoryDb.EligibilityCheckHashes.First(x =>
                x.EligibilityCheckHashID == baseItem.EligibilityCheckHashID);

        BaseHash.TimeStamp = BaseHash.TimeStamp.AddMonths(-12);
        await _fakeInMemoryDb.SaveChangesAsync();

        //post a second check so that New hash is used for outcome
        var responseNewPostCheck = await _sut.PostCheck(request);

        var newItem = _fakeInMemoryDb.CheckEligibilities.Include(x => x.EligibilityCheckHash)
            .FirstOrDefault(x => x.EligibilityCheckID == responseNewPostCheck.Id);
        newItem.EligibilityCheckHash.Should().BeNull();
        newItem.Status.Should().Be(CheckEligibilityStatus.queuedForProcessing);
    }

    [Test]
    public async Task Given_PostCheck_Status_should_Come_From_Hash()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;

        //Set UpValid hmrc check
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = request.NationalInsuranceNumber,
            Surname = request.LastName,
            DateOfBirth = DateTime.Parse(request.DateOfBirth)
        });
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act/Assert
        var response = await _sut.PostCheck(request);
        var baseItem = _fakeInMemoryDb.CheckEligibilities.FirstOrDefault(x => x.EligibilityCheckID == response.Id);
        baseItem.EligibilityCheckHashID.Should().BeNull();
        await _sut.ProcessCheck(response.Id, _fixture.Create<AuditData>());

        baseItem = _fakeInMemoryDb.CheckEligibilities.Include(x => x.EligibilityCheckHash)
            .FirstOrDefault(x => x.EligibilityCheckID == response.Id);
        baseItem.EligibilityCheckHash.Should().NotBeNull();
        var BaseHash = baseItem.EligibilityCheckHash;

        //post a second check so that BaseHash is used for outcome
        var responseNewPostCheck = await _sut.PostCheck(request);

        var newItem = _fakeInMemoryDb.CheckEligibilities.Include(x => x.EligibilityCheckHash)
            .FirstOrDefault(x => x.EligibilityCheckID == responseNewPostCheck.Id);
        newItem.EligibilityCheckHash.Should().NotBeNull();
        newItem.Status.Should().Be(BaseHash.Outcome);
    }

    [Test]
    public async Task Given_validRequest_PostFeature_Should_Return_id_HashShouldBeCreated()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;
        var key = string.IsNullOrEmpty(request.NationalInsuranceNumber)
            ? request.NationalAsylumSeekerServiceNumber
            : request.NationalInsuranceNumber;
        //Set UpValid hmrc check
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = request.NationalInsuranceNumber,
            Surname = request.LastName,
            DateOfBirth = DateTime.Parse(request.DateOfBirth)
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = _sut.PostCheck(request);
        var process = _sut.ProcessCheck(response.Result.Id, _fixture.Create<AuditData>());
        var item = _fakeInMemoryDb.CheckEligibilities.FirstOrDefault(x => x.EligibilityCheckID == response.Result.Id);
        var hash = CheckEligibilityGateway.GetHash(
            new CheckProcessData
            {
                DateOfBirth = request.DateOfBirth,
                LastName = request.LastName,
                NationalInsuranceNumber = request.NationalInsuranceNumber,
                NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
                Type = request.Type
            });
        // Assert
        _fakeInMemoryDb.EligibilityCheckHashes.First(x => x.Hash == hash).Should().NotBeNull();
    }


    [Test]
    public async Task Given_PostBulk_Should_Complete()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";
        request.NationalAsylumSeekerServiceNumber = null;
        var key = string.IsNullOrEmpty(request.NationalInsuranceNumber)
            ? request.NationalAsylumSeekerServiceNumber
            : request.NationalInsuranceNumber;
        //Set UpValid hmrc check
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = request.NationalInsuranceNumber,
            Surname = request.LastName,
            DateOfBirth = DateTime.Parse(request.DateOfBirth)
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        //
        var groupId = Guid.NewGuid().ToString();
        var data = new List<CheckEligibilityRequestData> { request };
        await _sut.PostCheck(data, groupId);
        Assert.Pass();
    }


    [Test]
    public void Given_validRequest_PostFeature_Should_Return_id()
    {
        // Arrange
        var request = _fixture.Create<CheckEligibilityRequestData>();
        request.DateOfBirth = "1970-02-01";

        // Act
        var response = _sut.PostCheck(request);

        // Assert
        response.Result.Id.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Given_InValidRequest_GetStatus_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.GetStatus(request);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public void Given_InValidRequest_GetStatus_Should_Return_status()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = _sut.GetStatus(item.EligibilityCheckID);

        // Assert
        _ = response.Result.ToString().Should().Be(item.Status.ToString());
    }

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
    public void Given_InValidRequest_GetBulkStatus_Should_Return_status()
    {
        // Arrange
        var items = _fixture.CreateMany<EligibilityCheck>();
        var guid = _fixture.Create<string>();
        foreach (var item in items) item.Group = guid;
        _fakeInMemoryDb.CheckEligibilities.AddRange(items);
        _fakeInMemoryDb.SaveChangesAsync();
        var results = _fakeInMemoryDb.CheckEligibilities
            .Where(x => x.Group == guid)
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
    public void Given_InValidRequest_Process_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.ProcessCheck(request, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public void Given_validRequest_StatusNot_queuedForProcessing_Process_Should_throwProcessException()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.eligible;
        item.Type = CheckEligibilityType.None;
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        act.Should().ThrowExactlyAsync<ProcessCheckException>();
    }


    [Test]
    public void Given_validRequest_StatusNot_queuedForProcessing_Process_Should_throwProcessException_InvalidStatus()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.eligible;
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        act.Should().ThrowExactlyAsync<ProcessCheckException>().Result
            .WithMessage($"Error checkItem {item.EligibilityCheckID} not queuedForProcessing. {item.Status}");
    }

    [Test]
    public async Task Given_validRequest_Process_Should_Return_updatedStatus_parentNotFound()
    {
        // Arrange

        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);

        await _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(CheckEligibilityStatus.parentNotFound.ToString());
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.parentNotFound);
    }

    [Test]
    public void Given_validRequest_HMRC_InvalidNI_Process_Should_Return_updatedStatus_parentNotFound()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(CheckEligibilityStatus.parentNotFound.ToString());
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.parentNotFound);
    }


    [Test]
    public void Given_validRequest_DWP_Soap_Process_Should_Return_updatedStatus_Eligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(true);
        var ecsSoapCheckResponse = new SoapFsmCheckRespone { Status = "1", ErrorCode = "0", Qualifier = "" };
        _moqDwpGateway.Setup(x => x.EcsFsmCheck(It.IsAny<CheckProcessData>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public void Given_validRequest_DWP_Soap_Process_Should_Return_updatedStatus_notEligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(true);
        var ecsSoapCheckResponse = new SoapFsmCheckRespone { Status = "0", ErrorCode = "0", Qualifier = "" };
        _moqDwpGateway.Setup(x => x.EcsFsmCheck(It.IsAny<CheckProcessData>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.notEligible);
    }

    [Test]
    public void Given_validRequest_DWP_Soap_Process_Should_Return_updatedStatus_parentNotFound()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(true);
        var ecsSoapCheckResponse = new SoapFsmCheckRespone
            { Status = "0", ErrorCode = "0", Qualifier = "No Trace - Check data" };
        _moqDwpGateway.Setup(x => x.EcsFsmCheck(It.IsAny<CheckProcessData>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.parentNotFound);
    }

    [Test]
    public void Given_validRequest_DWP_Soap_Process_Should_Return_Null_Error()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(true);

        _moqDwpGateway.Setup(x => x.EcsFsmCheck(It.IsAny<CheckProcessData>())).ReturnsAsync(value: null);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.queuedForProcessing);
    }

    [Test]
    public void Given_validRequest_DWP_Soap_Process_Should_Return_updatedStatus_Error()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(true);
        var ecsSoapCheckResponse = new SoapFsmCheckRespone
            { Status = "0", ErrorCode = "-1", Qualifier = "refer to admin" };
        _moqDwpGateway.Setup(x => x.EcsFsmCheck(It.IsAny<CheckProcessData>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.queuedForProcessing);
    }

    [Test]
    public void Given_validRequest_DWP_Process_Should_Return_updatedStatus_Eligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public void Given_validRequest_DWP_Process_Should_Return_updatedStatus_notEligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status404NotFound);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.notEligible);
    }

    [Test]
    public void Given_validRequest_DWP_Process_Should_Return_checkError()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.queuedForProcessing);
    }

    [Test]
    public async Task Given_validRequest_DWP_Process_Should_Return_500_Failure_status_is_NotUpdated()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(CheckEligibilityStatus.error.ToString());
        var result = new StatusCodeResult(StatusCodes.Status500InternalServerError);

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.queuedForProcessing);
    }

    [Test]
    public void Given_validRequest_HO_InvalidNASS_Process_Should_Return_updatedStatus_parentNotFound()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        fsm.NationalInsuranceNumber = null;
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.parentNotFound);
    }

    [Test]
    public void Given_validRequest_HMRC_Process_Should_Return_updatedStatus_eligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = fsm.NationalInsuranceNumber, Surname = fsm.LastName,
            DateOfBirth = DateTime.ParseExact(fsm.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None)
        });
        _fakeInMemoryDb.SaveChangesAsync();
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public void Given_SurnameCharacterMatchFails_HMRC_Process_Should_Return_updatedStatus_parentNotFound()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        fsm.DateOfBirth = "1990-01-01";
        var surnamevalid = "simpson";
        var surnameInvalid = "x" + surnamevalid;
        var dataItem = GetCheckProcessData(fsm);
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        item.Status = CheckEligibilityStatus.queuedForProcessing;

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = fsm.NationalInsuranceNumber, Surname = surnameInvalid, DateOfBirth =
                DateTime.ParseExact(dataItem.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None)
        });
        _fakeInMemoryDb.SaveChangesAsync();

        _moqDwpGateway.Setup(x => x.UseEcsforChecks).Returns(false);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>()))
            .ReturnsAsync(CheckEligibilityStatus.parentNotFound.ToString());
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.parentNotFound);
    }

    [Test]
    public async Task Given_SurnameCharacterMatchPasses_HMRC_Process_Should_Return_updatedStatus_eligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var surnamevalid = "simpson";
        fsm.LastName = surnamevalid;
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        item.Status = CheckEligibilityStatus.queuedForProcessing;

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.FreeSchoolMealsHMRC.Add(new FreeSchoolMealsHMRC
        {
            FreeSchoolMealsHMRCID = dataItem.NationalInsuranceNumber, Surname = surnamevalid,
            DateOfBirth = DateTime.ParseExact(dataItem.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None)
        });
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public async Task Given_validRequest_HO_Process_Should_Return_updatedStatus_eligible()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        fsm.NationalInsuranceNumber = string.Empty;

        var dataItem = GetCheckProcessData(fsm);
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.FreeSchoolMealsHO.Add(new FreeSchoolMealsHO
        {
            FreeSchoolMealsHOID = "123", NASS = dataItem.NationalAsylumSeekerServiceNumber,
            LastName = dataItem.LastName,
            DateOfBirth = DateTime.ParseExact(dataItem.DateOfBirth, "yyyy-MM-dd", null, DateTimeStyles.None)
        });
        _fakeInMemoryDb.SaveChangesAsync();
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public void Given_InValidRequest_GetItem_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.GetItem<CheckEligibilityItem>(request);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_GetItem_Should_Return_Item()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        var check = _fixture.Create<CheckEligibilityRequestData>();
        check.DateOfBirth = "1990-01-01";
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check));

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID);
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
        var check = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        check.ParentLastName = "simpson";
        item.CheckData = JsonConvert.SerializeObject(GetCheckProcessData(check));
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetItem<CheckEligibilityItem>(item.EligibilityCheckID);
        // Assert
        response.Should().BeOfType<CheckEligibilityItem>();
        response.EligibilityCode.Should().BeEquivalentTo(check.EligibilityCode);
        response.ValidityStartDate.Should().BeEquivalentTo(check.ValidityStartDate);
        response.ValidityEndDate.Should().BeEquivalentTo(check.ValidityEndDate);
        response.GracePeriodEndDate.Should().BeEquivalentTo(check.GracePeriodEndDate);
        response.ParentLastName.Should().BeEquivalentTo(check.ParentLastName.ToUpper());
        response.NationalInsuranceNumber.Should().BeEquivalentTo(check.NationalInsuranceNumber);
        response.ChildDateOfBirth.Should().BeEquivalentTo(check.ChildDateOfBirth);
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
    public void Given_ValidRequest_GetBulkCheckResults_Should_Return_Items()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var item = _fixture.Create<EligibilityCheck>();
        item.Group = groupId;
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData =
            """{"nationalInsuranceNumber": "AB123456C", "lastName": "Something", "dateOfBirth": "2000-01-01", "nationalAsylumSeekerServiceNumber": null}""";
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = _sut.GetBulkCheckResults<IList<CheckEligibilityItem>>(groupId);

        // Assert
        response.Result.Should().BeOfType<List<CheckEligibilityItem>>();
        response.Result.First().DateOfBirth.Should().Contain("2000-01-01");
        response.Result.First().NationalInsuranceNumber.Should().Contain("AB123456C");
        response.Result.First().LastName.Should().Contain("SOMETHING");
        response.Result.First().NationalAsylumSeekerServiceNumber.Should().BeNull();
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
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();

        var requestUpdateStatus = _fixture.Create<EligibilityCheckStatusData>();

        // Act
        var statusUpdate = await _sut.UpdateEligibilityCheckStatus(item.EligibilityCheckID, requestUpdateStatus);

        // Assert
        statusUpdate.Should().BeOfType<CheckEligibilityStatusResponse>();
        statusUpdate.Data.Status.Should().BeEquivalentTo(requestUpdateStatus.Status.ToString());
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth,
            LastName = request.LastName,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = new CheckEligibilityRequestData().Type
        };
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestWorkingFamiliesData request)
    {
        return new CheckProcessData
        {
            EligibilityCode = request.EligibilityCode,
            ParentLastName = request.ParentLastName,
            GracePeriodEndDate = request.GracePeriodEndDate,
            ValidityStartDate = request.ValidityStartDate,
            ValidityEndDate = request.ValidityEndDate,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            ChildDateOfBirth = request.ChildDateOfBirth,
            Type = CheckEligibilityType.WorkingFamilies
        };
    }
}