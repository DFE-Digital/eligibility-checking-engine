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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class HashServiceTests : TestBase.TestBase
{
    private readonly int _hashCheckDays = 7;
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private Mock<IAudit> _moqAudit;
    private HashGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var configForSmsApi = new Dictionary<string, string>
        {
            { "QueueFsmCheckStandard", "notSet" },
            { "HashCheckDays", _hashCheckDays.ToString() }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        
        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new HashGateway(new NullLoggerFactory(), _fakeInMemoryDb, _configuration, _moqAudit.Object);
    }

    [TearDown]
    public void Teardown()
    {
    }

    #region Commenting for build
    [Test]
    public async Task Given_validRequest_Create_Exists_Should_Return_Hash()
    {
        // Arrange
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);

        // Act
        var id = await _sut.Create(dataItem, CheckEligibilityStatus.parentNotFound, ProcessEligibilityCheckSource.HMRC,
            new AuditData());
        await _fakeInMemoryDb.SaveChangesAsync();

        var response = await _sut.Exists(dataItem);

        // Assert
        response.Should().BeOfType<EligibilityCheckHash>();
    }

    [Test]
    public async Task Given_HashIsOld_Exists_Should_Return_null()
    {
        // Arrange
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");
        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);


        var id = await _sut.Create(dataItem, CheckEligibilityStatus.parentNotFound, ProcessEligibilityCheckSource.HMRC,
            new AuditData());
        await _fakeInMemoryDb.SaveChangesAsync();
        var hashItem = _fakeInMemoryDb.EligibilityCheckHashes.First(x => x.EligibilityCheckHashID.Equals(id));
        hashItem.TimeStamp = hashItem.TimeStamp.AddDays(-(_hashCheckDays + 1));
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.Exists(dataItem);

        // Assert
        response.Should().BeNull();
    }
    #endregion

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