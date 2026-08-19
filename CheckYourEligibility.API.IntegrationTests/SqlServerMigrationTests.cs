using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.IntegrationTests;

[TestFixture]
public sealed class SqlServerMigrationTests
{
    [Test]
    public async Task Migrations_Create_And_Seed_EligibilityCodeRange()
    {
        await using var context = SqlServerFixture.CreateContext();

        var range = await context.EligibilityCodeRanges.SingleAsync();

        range.EligibilityCodeRangeId.Should().Be(1);
        range.StartRange.Should().Be(40000000001);
        range.EndRange.Should().Be(49999999999);
        range.NextAvailableCode.Should().Be(40000000001);
        range.RowVersion.Should().NotBeNullOrEmpty();
    }
}