using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ManagedBulkInsertTests
{
    private const string Partition = "ManagedBulkInsertTests";

    [SetUp]
    [TearDown]
    public async Task Cleanup()
    {
        await using var context = SqlServerFixture.CreateContext();
        await context.RateLimitEvents
            .Where(e => e.PartitionName == Partition)
            .ExecuteDeleteAsync();
    }

    private static List<RateLimitEvent> BuildEvents(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new RateLimitEvent
            {
                RateLimitEventID = Guid.NewGuid().ToString(),
                TimeStamp = DateTime.UtcNow,
                PartitionName = Partition,
                QuerySize = i,
                Accepted = true
            })
            .ToList();

    private static async Task<int> CountAsync()
    {
        await using var context = SqlServerFixture.CreateContext();
        return await context.RateLimitEvents
            .CountAsync(e => e.PartitionName == Partition);
    }

    [Test]
    public async Task ManagedBulkInsert_MultipleBatches_InsertsAllRows()
    {
        await using var context = SqlServerFixture.CreateContext();

        context.ManagedBulkInsert(BuildEvents(25),
            new ManagedBulkInsertOptions { BatchSize = 10 });

        (await CountAsync()).Should().Be(25);
    }

    [Test]
    public async Task ManagedBulkInsert_EmptyData_InsertsNothing()
    {
        await using var context = SqlServerFixture.CreateContext();

        context.ManagedBulkInsert(new List<RateLimitEvent>());

        (await CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task ManagedBulkInsert_FailingBatch_KeepsEarlierBatchesAndRollsBackFailedBatch()
    {
        await using var seedContext = SqlServerFixture.CreateContext();
        var existing = BuildEvents(1)[0];
        seedContext.ManagedBulkInsert(new[] { existing });

        // Batch 1 (3 rows) is valid; batch 2 contains a duplicate key and always fails.
        var data = BuildEvents(3);
        var batch2 = BuildEvents(3);
        batch2[1].RateLimitEventID = existing.RateLimitEventID;
        data.AddRange(batch2);

        await using var context = SqlServerFixture.CreateContext();
        var act = () => context.ManagedBulkInsert(data,
            new ManagedBulkInsertOptions { BatchSize = 3, MaxRetries = 2, RetryDelaySeconds = 0 });

        act.Should().Throw<Exception>();

        // 1 seeded + 3 from the committed first batch; failed batch fully rolled back
        (await CountAsync()).Should().Be(4);
    }
}
