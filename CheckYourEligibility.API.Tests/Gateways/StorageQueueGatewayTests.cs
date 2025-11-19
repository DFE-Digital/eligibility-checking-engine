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

public class StorageQueueGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private HashGateway _hashGateway;
    private IMapper _mapper;
    private Mock<IAudit> _moqAudit;
    private Mock<IEcsAdapter> _moqEcsGateway;
    private Mock<QueueServiceClient> _queueClientService;
    private Mock<ICheckingEngine> _moqCheckingEngineGateway;
    private Mock<ICheckEligibility> _moqCheckEligibilityGateway;
    private Mock<IDwpAdapter> _moqDwpGateway;
    private Mock<IStorageQueueMessage> _moqStorageQueueGateway;
    private StorageQueueGateway _sut;

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
        _moqCheckEligibilityGateway = new Mock<ICheckEligibility>();
        _moqCheckingEngineGateway = new Mock<ICheckingEngine>();
        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);
        _hashGateway = new HashGateway(new NullLoggerFactory(), _fakeInMemoryDb, _configuration, _moqAudit.Object);
        _queueClientService = new Mock<QueueServiceClient>();


        _sut = new StorageQueueGateway(new NullLoggerFactory(), _fakeInMemoryDb, _queueClientService.Object,
            _configuration, _moqCheckEligibilityGateway.Object, _moqCheckingEngineGateway.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }
}