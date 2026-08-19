using CheckYourEligibility.API.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CheckYourEligibility.API.IntegrationTests;

[SetUpFixture]
public sealed class SqlServerFixture
{
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04";

    private MsSqlContainer? _container;

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
    }

    [OneTimeTearDown]
    public async Task StopSqlServer()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    internal static string ConnectionString { get; private set; } =
        string.Empty;

    internal static EligibilityCheckContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<EligibilityCheckContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        return new EligibilityCheckContext(options);
    }
}