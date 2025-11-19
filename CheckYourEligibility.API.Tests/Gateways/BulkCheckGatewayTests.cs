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