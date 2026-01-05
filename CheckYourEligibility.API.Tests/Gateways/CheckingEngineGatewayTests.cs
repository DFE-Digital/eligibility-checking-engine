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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Tests;

public class CheckingEngineGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private HashGateway _hashGateway;
    private IMapper _mapper;
    private Mock<IAudit> _moqAudit;
    private Mock<IEcsAdapter> _moqEcsGateway;
    private Mock<IDwpAdapter> _moqDwpGateway;
    private Mock<IStorageQueueMessage> _moqStorageQueueGateway;
    private CheckingEngineGateway _sut;

    [SetUp]
    public async Task Setup()
    {
        var databaseName = $"FakeInMemoryDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName)
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


        _sut = new CheckingEngineGateway(new NullLoggerFactory(), _fakeInMemoryDb,
            _configuration, _moqEcsGateway.Object, _moqDwpGateway.Object, _hashGateway);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
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

    [Ignore("Temporqarily disabled")]
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
        var citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
        citizenResponse.Guid = string.Empty;
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
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
        item.EligibilityCheckID = Guid.NewGuid().ToString();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        // Set navigation properties to null to avoid creating additional entities
        item.EligibilityCheckHash = null;
        item.EligibilityCheckHashID = null;
        item.BulkCheck = null;
        
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.Guid = string.Empty;
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
        fsm.DateOfBirth = "1990-01-01";
        fsm.Type = CheckEligibilityType.FreeSchoolMeals; // Force FSM type for this test
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChanges();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(),It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
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
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse { Status = "1", ErrorCode = "0", Qualifier = "" };
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public async Task Given_ECS_Conflict_Process_Should_Return_ECS_Result_Eligible()
    {

        var ecsConflict = _fixture.Create<ECSConflict>();
        var capiResult = new StatusCodeResult(StatusCodes.Status404NotFound);
        var ecsSoapCheckResponse = new SoapCheckResponse { Status = "1", ErrorCode = "0", Qualifier = "" };
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);

        var item = _fixture.Create<EligibilityCheck>();
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var audit = _fixture.Create<Audit>();
        audit.TypeID = item.EligibilityCheckID;
        _fakeInMemoryDb.Audits.Add(audit);

        CAPICitizenResponse citizenResponse =  _fixture.Create<CAPICitizenResponse>();      
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("validate");
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((capiResult, string.Empty));
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        //Assert
        // Should return ECS result
        response.Should().Be(CheckEligibilityStatus.eligible);
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
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse { Status = "0", ErrorCode = "0", Qualifier = "" };
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        //_moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");


        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.notEligible);
    }

    [Test]
    public void Given_validRequest_DWP_Soap_Pending_Keep_Checking_Process_Should_Return_updatedStatus_notEligible()
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
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse { Status = "0", ErrorCode = "0", Qualifier = "Pending - Keep checking" };
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Result.Should().Be(CheckEligibilityStatus.notEligible);
    }

    [Test]
    public void Given_validRequest_DWP_Soap_Manual_Process_Process_Should_Return_updatedStatus_notEligible()
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
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse { Status = "0", ErrorCode = "0", Qualifier = "Manual process" };
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
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
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse
            { Status = "0", ErrorCode = "0", Qualifier = "No Trace - Check data" };
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
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
        fsm.Type = CheckEligibilityType.FreeSchoolMeals;
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");

        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(value: null);
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
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse
            { Status = "0", ErrorCode = "-1", Qualifier = "refer to admin" };
        _moqEcsGateway.Setup(x => x.EcsCheck(It.IsAny<CheckProcessData>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
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
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(),It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        var result = new StatusCodeResult(StatusCodes.Status200OK);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((result, string.Empty));
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
        var citizenResponse = _fixture.Create<CAPICitizenResponse>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        var result = new StatusCodeResult(StatusCodes.Status404NotFound);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((result, string.Empty));
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
        item.Type = CheckEligibilityType.FreeSchoolMeals;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();

        _fakeInMemoryDb.CheckEligibilities.Add(item);
        _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        var result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(),It.IsAny<string>()))
            .ReturnsAsync((result, string.Empty));
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
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
        citizenResponse.Guid = string.Empty;
        var item = _fixture.Create<EligibilityCheck>();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        fsm.Type = CheckEligibilityType.FreeSchoolMeals;
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        var result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

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
        item.Type = CheckEligibilityType.FreeSchoolMeals;
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
        item.Type = CheckEligibilityType.FreeSchoolMeals;
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
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
        citizenResponse.Guid = string.Empty;

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
        _moqEcsGateway.Setup(x => x.UseEcsforChecks).Returns("false");
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
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
        item.EligibilityCheckID = Guid.NewGuid().ToString();
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        // Set navigation properties to null to avoid creating additional entities
        item.EligibilityCheckHash = null;
        item.EligibilityCheckHashID = null;
        item.BulkCheck = null;
        
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        fsm.Type = CheckEligibilityType.FreeSchoolMeals; // Force FSM type for this test
        var surnamevalid = "simpson";
        fsm.LastName = surnamevalid;
        var dataItem = GetCheckProcessData(fsm);
        item.Type = fsm.Type;
        item.CheckData = JsonConvert.SerializeObject(dataItem);

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
    public async Task Given_validRequest_Process_Should_Return_updatedStatus_notEligble()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var wf = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        wf.DateOfBirth = "2022-01-01";
        wf.NationalInsuranceNumber = "AB123456C";
        wf.EligibilityCode = "50012345678";
        wf.LastName = "smith";
        var dataItem = GetCheckProcessData(wf);
        item.Type = CheckEligibilityType.WorkingFamilies;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var wfEvent = _fixture.Create<WorkingFamiliesEvent>();
        wfEvent.EligibilityCode = "50012345678";
        wfEvent.ParentNationalInsuranceNumber = "AB123456C";
        wfEvent.ParentLastName = "smith";
        wfEvent.ChildDateOfBirth = new DateTime(2022, 1, 1);
        wfEvent.ValidityStartDate = DateTime.Today.AddDays(-2);
        wfEvent.ValidityEndDate = DateTime.Today.AddDays(-1);
        wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(-1);
        _fakeInMemoryDb.WorkingFamiliesEvents.Add(wfEvent);
        await _fakeInMemoryDb.SaveChangesAsync();
        
        
        var soapResponse = _fixture.Create<SoapCheckResponse>();
        soapResponse.ParentSurname = "smith";
        soapResponse.ValidityStartDate = DateTime.Today.AddDays(-2).ToString();
        soapResponse.ValidityEndDate = DateTime.Today.AddDays(-1).ToString();
        soapResponse.GracePeriodEndDate = DateTime.Today.AddDays(-1).ToString();
        
        _moqEcsGateway.Setup(x => x.UseEcsforChecksWF).Returns("true");
        _moqEcsGateway.Setup(x => x.EcsWFCheck(It.IsAny<CheckProcessData>(), It.IsAny<string>())).ReturnsAsync(soapResponse);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.notEligible);
    }

    [Test]
    public async Task Given_validRequest_Process_Should_Return_LastName_From_Request()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var wf = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        wf.DateOfBirth = "2022-01-01";
        wf.NationalInsuranceNumber = "AB123456C";
        wf.EligibilityCode = "50012345678";
        wf.LastName = "smith";
        var dataItem = GetCheckProcessData(wf);
        item.Type = CheckEligibilityType.WorkingFamilies;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var wfEvent = _fixture.Create<WorkingFamiliesEvent>();
        wfEvent.EligibilityCode = "50012345678";
        wfEvent.ParentNationalInsuranceNumber = "AB123456C";
        wfEvent.ParentLastName = "smith";
        wfEvent.ChildDateOfBirth = new DateTime(2022, 1, 1);
        wfEvent.ValidityStartDate = DateTime.Today.AddDays(-2);
        wfEvent.ValidityEndDate = DateTime.Today.AddDays(-1);
        wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(-1);
        _fakeInMemoryDb.WorkingFamiliesEvents.Add(wfEvent);
        await _fakeInMemoryDb.SaveChangesAsync();
        
        
        var soapResponse = _fixture.Create<SoapCheckResponse>();
        soapResponse.Status = "1";
        soapResponse.ErrorCode = "0";
        soapResponse.Qualifier = "";
        soapResponse.ParentSurname = "";
        soapResponse.ValidityStartDate = DateTime.Today.AddDays(-2).ToString();
        soapResponse.ValidityEndDate = DateTime.Today.AddDays(1).ToString();
        soapResponse.GracePeriodEndDate = DateTime.Today.AddDays(1).ToString();
        
        _moqEcsGateway.Setup(x => x.UseEcsforChecksWF).Returns("true");
        _moqEcsGateway.Setup(x => x.EcsWFCheck(It.IsAny<CheckProcessData>(), It.IsAny<string>())).ReturnsAsync(soapResponse);
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.eligible);
        JsonConvert.DeserializeObject<CheckProcessData>(item.CheckData)?.LastName.Should().Be("smith");
        //item.CheckData.Should().Be(JsonConvert.SerializeObject(dataItem)); //TODO: IT shouldn't be this. Just checking that this is still the right reference to the object
    }

    [Test]
    public async Task Given_validRequest_dobNonMatch_Process_Should_Return_updatedStatus_notFound()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var wf = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        wf.DateOfBirth = "2022-01-01";
        wf.NationalInsuranceNumber = "AB123456C";
        wf.EligibilityCode = "50012345678";
        wf.LastName = "smith";
        var dataItem = GetCheckProcessData(wf);
        item.Type = CheckEligibilityType.WorkingFamilies;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var wfEvent = _fixture.Create<WorkingFamiliesEvent>();
        wfEvent.EligibilityCode = "50012345678";
        wfEvent.ParentNationalInsuranceNumber = "AB123456C";
        wfEvent.ParentLastName = "smith";
        wfEvent.ChildDateOfBirth = new DateTime(1980, 1, 1);
        wfEvent.ValidityEndDate = DateTime.Today.AddDays(-1);
        wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(-1);
        _fakeInMemoryDb.WorkingFamiliesEvents.Add(wfEvent);
        await _fakeInMemoryDb.SaveChangesAsync();

        _moqEcsGateway.Setup(x => x.UseEcsforChecksWF).Returns("false");
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");
        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.notFound);
    }

    [Test]
    public async Task Given_validRequest_lastNameNonMatch_Process_Should_Return_updatedStatus_notFound()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var wf = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        wf.DateOfBirth = "2022-01-01";
        wf.NationalInsuranceNumber = "AB123456C";
        wf.EligibilityCode = "50012345678";
        wf.LastName = "smith";
        var dataItem = GetCheckProcessData(wf);
        item.Type = CheckEligibilityType.WorkingFamilies;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var wfEvent = _fixture.Create<WorkingFamiliesEvent>();
        wfEvent.EligibilityCode = "50012345678";
        wfEvent.ParentNationalInsuranceNumber = "AB123456C";
        wfEvent.ParentLastName = "doe";
        wfEvent.ChildDateOfBirth = new DateTime(2022, 1, 1);
        wfEvent.ValidityEndDate = DateTime.Today.AddDays(-1);
        wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(-1);
        _fakeInMemoryDb.WorkingFamiliesEvents.Add(wfEvent);
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");
        _moqEcsGateway.Setup(x => x.UseEcsforChecksWF).Returns("false");
        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.notFound);
    }

    [Test]
    public async Task Given_validRequest_lastNameNonMatch_Process_Should_Return_updatedStatus_eligibles()
    {
        // Arrange
        var item = _fixture.Create<EligibilityCheck>();
        var wf = _fixture.Create<CheckEligibilityRequestWorkingFamiliesData>();
        wf.DateOfBirth = "2022-01-01";
        wf.NationalInsuranceNumber = "AB123456C";
        wf.EligibilityCode = "50012345678";
        wf.LastName = "smith";
        var dataItem = GetCheckProcessData(wf);
        item.Type = CheckEligibilityType.WorkingFamilies;
        item.Status = CheckEligibilityStatus.queuedForProcessing;
        item.CheckData = JsonConvert.SerializeObject(dataItem);
        _fakeInMemoryDb.CheckEligibilities.Add(item);

        var wfEvent = _fixture.Create<WorkingFamiliesEvent>();
        wfEvent.EligibilityCode = "50012345678";
        wfEvent.ParentNationalInsuranceNumber = "AB123456C";
        wfEvent.ParentLastName = "doe";
        wfEvent.ChildDateOfBirth = new DateTime(2022, 1, 1);
        wfEvent.ValidityEndDate = DateTime.Today.AddDays(-1);
        wfEvent.ValidityStartDate = DateTime.Today.AddDays(-2);
        wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(-1);
        _fakeInMemoryDb.WorkingFamiliesEvents.Add(wfEvent);
        await _fakeInMemoryDb.SaveChangesAsync();
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");
        _moqEcsGateway.Setup(x => x.UseEcsforChecksWF).Returns("true");
        var ecsSoapCheckResponse = new SoapCheckResponse { Status = "1", ErrorCode = "0", Qualifier = "", ValidityEndDate = DateTime.Today.AddDays(-1).ToString(), ValidityStartDate = DateTime.Today.AddDays(-2).ToString(), GracePeriodEndDate = DateTime.Today.AddDays(1).ToString() };
        _moqEcsGateway.Setup(x => x.EcsWFCheck(It.IsAny<CheckProcessData>(), It.IsAny<string>())).ReturnsAsync(ecsSoapCheckResponse);
        // Act
        var response = await _sut.ProcessCheck(item.EligibilityCheckID, _fixture.Create<AuditData>());

        // Assert
        response.Should().Be(CheckEligibilityStatus.eligible);
    }

    [Test]
    public async Task Given_ECE_Failed_Making_Request_To_CAPI_Should_Return_Error()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.Guid = string.Empty;
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
        citizenResponse.CAPIResponseCode = 0;
        citizenResponse.CAPIEndpoint = "/v2/citizens/match";
        citizenResponse.Reason = "ECE failed making a requet to GET citizen.";
        string correlationId = Guid.NewGuid().ToString();

        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);
        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo(citizenResponse.CAPIEndpoint);
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.error);
        response.Reason.Should().Contain(citizenResponse.Reason);
        response.CAPIResponseCode.Should().Be(citizenResponse.CAPIResponseCode);
    }
    [Test]
    public async Task Given_Citizen_Request_Failed_Should_Return_Error()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.Guid = string.Empty;
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
        citizenResponse.CAPIResponseCode = HttpStatusCode.InternalServerError;
        citizenResponse.CAPIEndpoint = "/v2/citizens/match";
        citizenResponse.Reason = "CAPI failed getting citizen.";
        string correlationId = Guid.NewGuid().ToString();

        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);
        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo(citizenResponse.CAPIEndpoint);
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.error);
        response.Reason.Should().Contain(citizenResponse.Reason);
        response.CAPIResponseCode.Should().Be(citizenResponse.CAPIResponseCode);
    }
    [Test]
    public async Task Given_Citizen_Has_Possible_Conflict_Should_Return_Error()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.Guid = string.Empty;
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
        citizenResponse.CAPIResponseCode = HttpStatusCode.UnprocessableEntity;
        citizenResponse.CAPIEndpoint = "/v2/citizens/match";
        citizenResponse.Reason = "Possible conflict";
        string correlationId = Guid.NewGuid().ToString();

        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);
        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo(citizenResponse.CAPIEndpoint);
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.error);
        response.Reason.Should().Contain(citizenResponse.Reason);
        response.CAPIResponseCode.Should().Be(citizenResponse.CAPIResponseCode);
    }
    [Test]
    public async Task Given_Citizen_Is_Not_Found_Should_Return_ParentNotFound()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        citizenResponse.Guid = string.Empty;
        citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
        citizenResponse.CAPIResponseCode = HttpStatusCode.NotFound;
        citizenResponse.CAPIEndpoint = "/v2/citizens/match";
        citizenResponse.Reason = "No citizen found";
        string correlationId = Guid.NewGuid().ToString();
        
        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);
        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo(citizenResponse.CAPIEndpoint);
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.parentNotFound);
        response.Reason.Should().Contain(citizenResponse.Reason);
        response.CAPIResponseCode.Should().Be(HttpStatusCode.NotFound);
    }
    [Test]
    public async Task Given_Citizen_Is_Found_Claim_Request_Attempt_Fails_Should_Return_Error()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        string correlationId = Guid.NewGuid().ToString();
        string reason = "ECE failed to POST to CAPI.";
        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((new InternalServerErrorResult(), reason));
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);

        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo($"v2/citizens/{citizenResponse.Guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based");
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.error);
        response.Reason.Should().Contain(reason);
        response.CAPIResponseCode.Should().Be(HttpStatusCode.InternalServerError);
    }
    [Test]
    public async Task Given_Citizen_Is_Found_Claim_Returns_Server_Error__Should_Return_Error()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();

        string correlationId = Guid.NewGuid().ToString();
        string reason = "Get CAPI citizen claim failed.";
        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((new InternalServerErrorResult(), reason));
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);

        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo($"v2/citizens/{citizenResponse.Guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based");
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.error);
        response.Reason.Should().Contain(reason);
        response.CAPIResponseCode.Should().Be(HttpStatusCode.InternalServerError);
    }
    [Test]
    public async Task Given_Citizen_Is_Found_Claim_Returns_200_Check_Benefit_Logic_Entitlemment_Is_False_Should_Return_Not_Eligible()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        string correlationId = Guid.NewGuid().ToString();
        string reason = "CAPI returned status 200, but no benefits found after using business logic.";
        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((new NotFoundResult(), reason));
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);

        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo($"v2/citizens/{citizenResponse.Guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based");
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.notEligible);
        response.Reason.Should().Be(reason);
        response.CAPIResponseCode.Should().Be(HttpStatusCode.NotFound);
    }
    [Test]
    public async Task Given_Citizen_Is_Found_Claim_Is_Not_Found_Should_Return_Not_Eligible()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        string correlationId = Guid.NewGuid().ToString();
        string reason = "CAPI did not find any data for this citizen";
        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((new NotFoundResult(), reason));
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);

        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo($"v2/citizens/{citizenResponse.Guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based");
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.notEligible);
        response.Reason.Should().Be(reason);
        response.CAPIResponseCode.Should().Be(HttpStatusCode.NotFound);
    }
    [Test]
    public async Task Given_Citizen_Is_Found_Claim_Is_Found_Result_Should_Return_Eligible()
    {
        CheckProcessData checkProcessData = _fixture.Create<CheckProcessData>();
        CAPICitizenResponse citizenResponse = _fixture.Create<CAPICitizenResponse>();
        string correlationId = Guid.NewGuid().ToString();
        string reason =
            "CAPI confirms citizen has benefit of type -" +
            "employment_support_allowance_income_based" +
            "or income_support, " +
            "or job_seekers_allowance_income_based, " +
            "or pensions_credit " +
            "or universal_credit ";
        // Arrange
        _moqDwpGateway.Setup(x => x.GetCitizen(It.IsAny<CitizenMatchRequest>(), It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync(citizenResponse);
        _moqDwpGateway.Setup(x => x.GetCitizenClaims(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CheckEligibilityType>(), It.IsAny<string>()))
            .ReturnsAsync((new OkResult(), reason));
        // Act
        CAPIClaimResponse response = await _sut.DwpCitizenCheck(checkProcessData, CheckEligibilityStatus.parentNotFound, correlationId);

        // Assert
        response.CAPIEndpoint.Should().BeEquivalentTo($"v2/citizens/{citizenResponse.Guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based");
        response.CheckEligibilityStatus.Should().Be(CheckEligibilityStatus.eligible);
        response.Reason.Should().Be(reason);
        response.CAPIResponseCode.Should().Be(HttpStatusCode.OK);
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
}