using FluentAssertions;

namespace CheckYourEligibility.API.IntegrationTests;

[TestFixture]
public sealed class SqlServerMigrationTests
{
    [Test]
    public void Migrations_Create_And_Seed_EligibilityCodeRange()
    {
        SqlServerFixture.InitialRangeStart
            .Should().Be(40000000001);

        SqlServerFixture.InitialRangeEnd
            .Should().Be(49999999999);

        SqlServerFixture.InitialNextAvailableCode
            .Should().Be(40000000001);

        SqlServerFixture.InitialRowVersion
            .Should().NotBeNullOrEmpty();
    }
}