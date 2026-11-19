using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.Core.Tests.Infrastructure;

[TestFixture]
public class ManagedBulkInsertValidationTests
{
    private EligibilityCheckContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseSqlServer("Server=invalid;Database=invalid;")
            .Options;
        _context = new EligibilityCheckContext(options);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void Options_HaveExpectedDefaults()
    {
        var options = new ManagedBulkInsertOptions();

        options.BatchSize.Should().Be(1000);
        options.TimeoutSeconds.Should().Be(120);
        options.MaxRetries.Should().Be(3);
        options.RetryDelaySeconds.Should().Be(2);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ManagedBulkInsert_InvalidBatchSize_Throws(int batchSize)
    {
        var act = () => _context.ManagedBulkInsert(new List<RateLimitEvent>(),
            new ManagedBulkInsertOptions { BatchSize = batchSize });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ManagedBulkInsert_InvalidMaxRetries_Throws(int maxRetries)
    {
        var act = () => _context.ManagedBulkInsert(new List<RateLimitEvent>(),
            new ManagedBulkInsertOptions { MaxRetries = maxRetries });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
