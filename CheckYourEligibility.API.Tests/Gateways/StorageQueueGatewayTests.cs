using AutoMapper;
using Azure.Storage.Queues;
using CheckYourEligibility.Core.Adapters;
using CheckYourEligibility.Core.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class StorageQueueGatewayTests : TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

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
    private Mock<IStorageQueue> _moqStorageQueueGateway;
    private StorageQueueGateway _sut;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(StorageQueueGatewayTests),
                InMemoryDatabaseRoot)
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

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
        { "Dwp:UseEcsforChecksWF", "false" }
    };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();

        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _moqEcsGateway = new Mock<IEcsAdapter>(MockBehavior.Strict);
        _moqDwpGateway = new Mock<IDwpAdapter>(MockBehavior.Strict);
        _moqStorageQueueGateway = new Mock<IStorageQueue>();
        _moqCheckEligibilityGateway = new Mock<ICheckEligibility>();
        _moqCheckingEngineGateway = new Mock<ICheckingEngine>();
        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);

        _hashGateway = new HashGateway(
            new NullLoggerFactory(),
            _fakeInMemoryDb,
            _configuration,
            _moqAudit.Object);

        _queueClientService = new Mock<QueueServiceClient>();

        _sut = new StorageQueueGateway(
            new NullLoggerFactory(),
            _queueClientService.Object,
            _configuration);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }
}