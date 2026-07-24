using AutoFixture;
using Azure.Storage.Queues.Models;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ProcessEligibilityBulkCheckUseCaseBulkTests : TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    private Mock<IStorageQueue> _mockGateway;
    private Mock<ILogger<ProcessEligibilityBulkCheckUseCase>> _mockLogger;
    private Mock<IConfiguration> _mockConf;
    private Mock<ICheckEligibility> _mockCheckEligibilityGateway;
    private Mock<IDbContextFactory<EligibilityCheckContext>> _mockDbContextFactory;
    private Mock<IProcessEligibilityCheckUseCase> _mockProcessEligibilityCheckUseCase;

    private ProcessEligibilityBulkCheckUseCase _sut;

    private DbContextOptions<EligibilityCheckContext> _dbOptions;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IStorageQueue>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ProcessEligibilityBulkCheckUseCase>>(MockBehavior.Loose);
        _mockConf = new Mock<IConfiguration>(MockBehavior.Loose);
        _mockCheckEligibilityGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockDbContextFactory = new Mock<IDbContextFactory<EligibilityCheckContext>>(MockBehavior.Strict);
        _mockProcessEligibilityCheckUseCase = new Mock<IProcessEligibilityCheckUseCase>(MockBehavior.Strict);

        _dbOptions = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(ProcessEligibilityBulkCheckUseCaseBulkTests),
                InMemoryDatabaseRoot)
            .Options;

        using (var db = new EligibilityCheckContext(_dbOptions))
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        }

        _mockDbContextFactory
            .Setup(x => x.CreateDbContext())
            .Returns(() => new EligibilityCheckContext(_dbOptions));

        _sut = new ProcessEligibilityBulkCheckUseCase(
            _mockGateway.Object,
            _mockLogger.Object,
            _mockProcessEligibilityCheckUseCase.Object,
            _mockConf.Object,
            _mockCheckEligibilityGateway.Object,
            _mockDbContextFactory.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_does_not_complete_bulk_when_checks_already_pending()
    {
        var bulkId = "bulk-simple";

        // One already completed, one still queued
        SeedBulk(bulkId,
            ("check-1", CheckEligibilityStatus.eligible),
            ("check-2", CheckEligibilityStatus.queuedForProcessing)
        );

        _mockGateway
            .Setup(x => x.ProcessQueueAsync("queue"))
            .ReturnsAsync(new[]
            {
                CreateMessage("check-1")
            });

        _mockGateway
            .Setup(x => x.DeleteMessageAsync(It.IsAny<QueueMessage>(), "queue"))
            .Returns(Task.CompletedTask);

        
        _mockProcessEligibilityCheckUseCase
            .Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<EligibilityCheckContext>()))
            .ReturnsAsync(new CheckEligibilityStatusResponse
            {
                Data = new StatusValue
                {
                    Status = CheckEligibilityStatus.eligible.ToString()
                }
            });

        await _sut.Execute("queue");

        using var db = new EligibilityCheckContext(_dbOptions);
        var bulk = db.BulkChecks.First(x => x.BulkCheckID == bulkId);

        bulk.Status.Should().Be(BulkCheckStatus.InProgress);
        bulk.CompletedDate.Should().BeNull();
    }

    [Test]
    public async Task Execute_does_not_complete_bulk_when_checks_remaining()
    {
        var bulkId = "bulk-partial";

        // Both start queued
        SeedBulk(bulkId,
            ("check-1", CheckEligibilityStatus.queuedForProcessing),
            ("check-2", CheckEligibilityStatus.queuedForProcessing)
        );

        SetupDbUpdatingProcessing();

        _mockGateway
            .Setup(x => x.ProcessQueueAsync("queue"))
            .ReturnsAsync(new[]
            {
                CreateMessage("check-1") // only one processed
            });

        _mockGateway
            .Setup(x => x.DeleteMessageAsync(It.IsAny<QueueMessage>(), "queue"))
            .Returns(Task.CompletedTask);

        await _sut.Execute("queue");

        using var db = new EligibilityCheckContext(_dbOptions);
        var bulk = db.BulkChecks.First(x => x.BulkCheckID == bulkId);

        bulk.Status.Should().Be(BulkCheckStatus.InProgress);
        bulk.CompletedDate.Should().BeNull();
    }

    [Test]
    public async Task Execute_completes_bulk_when_all_checks_processed()
    {
        var bulkId = "bulk-complete";

        // Both start queued
        SeedBulk(bulkId,
            ("check-1", CheckEligibilityStatus.queuedForProcessing),
            ("check-2", CheckEligibilityStatus.queuedForProcessing)
        );

        SetupDbUpdatingProcessing();

        _mockGateway
            .Setup(x => x.ProcessQueueAsync("queue"))
            .ReturnsAsync(new[]
            {
                CreateMessage("check-1"),
                CreateMessage("check-2")
            });

        _mockGateway
            .Setup(x => x.DeleteMessageAsync(It.IsAny<QueueMessage>(), "queue"))
            .Returns(Task.CompletedTask);

        await _sut.Execute("queue");

        using var db = new EligibilityCheckContext(_dbOptions);
        var bulk = db.BulkChecks.First(x => x.BulkCheckID == bulkId);

        bulk.Status.Should().Be(BulkCheckStatus.Completed);
        bulk.CompletedDate.Should().NotBeNull();
    }

    #region Helpers
    private void SeedBulk(string bulkId, params (string id, CheckEligibilityStatus status)[] checks)
    {
        using var db = new EligibilityCheckContext(_dbOptions);

        db.BulkChecks.Add(new Core.Domain.BulkCheck
        {
            BulkCheckID = bulkId,
            Status = BulkCheckStatus.InProgress
        });

        foreach (var c in checks)
        {
            db.CheckEligibilities.Add(new Core.Domain.EligibilityCheck    
            {
                EligibilityCheckID = c.id,
                BulkCheckID = bulkId,
                Status = c.status
            });
        }

        db.SaveChanges();
    }

    private QueueMessage CreateMessage(string checkId, int dequeueCount = 1)
    {
        var json = JsonConvert.SerializeObject(new QueueMessageCheck { Guid = checkId });

        return QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: Guid.NewGuid().ToString(),
            body: BinaryData.FromString(json),
            dequeueCount: dequeueCount,
            insertedOn: DateTimeOffset.UtcNow,
            expiresOn: DateTimeOffset.UtcNow.AddDays(1),
            nextVisibleOn: DateTimeOffset.UtcNow
        );
    }

    private void SetupDbUpdatingProcessing()
    {
        _mockProcessEligibilityCheckUseCase
            .Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<EligibilityCheckContext>()))
            .ReturnsAsync((string checkId, EligibilityCheckContext db) =>
            {
                var check = db.CheckEligibilities.First(x => x.EligibilityCheckID == checkId);
                check.Status = CheckEligibilityStatus.eligible;
                db.SaveChanges();

                return new CheckEligibilityStatusResponse
                {
                    Data = new StatusValue
                    {
                        Status = CheckEligibilityStatus.eligible.ToString()
                    }
                };
            });
    }
    
    #endregion
}
