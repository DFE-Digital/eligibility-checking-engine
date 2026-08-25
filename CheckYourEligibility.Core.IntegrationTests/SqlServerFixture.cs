using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain.Enums.WorkingFamilies;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CheckYourEligibility.Core.IntegrationTests;

[SetUpFixture]
public sealed class SqlServerFixture
{
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04";

    private MsSqlContainer? _container;

    internal static string ConnectionString { get; private set; } =
        string.Empty;

    internal static long InitialRangeStart { get; private set; }

    internal static long InitialRangeEnd { get; private set; }

    internal static long InitialNextAvailableCode { get; private set; }

    internal static EligibilityCodeType InitialRangeName { get; private set; }

    [OneTimeSetUp]
    public async Task StartSqlServer()
    {
        _container = new MsSqlBuilder()
            .WithImage(SqlServerImage)
            .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        // Capture the original migration state before any tests
        // allocate codes and change NextAvailableCode.
        var initialRange = await context.EligibilityCodeRanges
            .AsNoTracking()
            .SingleAsync();

        InitialRangeStart = initialRange.StartRange;
        InitialRangeEnd = initialRange.EndRange;
        InitialNextAvailableCode =
            initialRange.NextAvailableCode;
        InitialRangeName = initialRange.Name;
    }

    [OneTimeTearDown]
    public async Task StopSqlServer()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    internal static EligibilityCheckContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<EligibilityCheckContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        return new EligibilityCheckContext(options);
    }
}