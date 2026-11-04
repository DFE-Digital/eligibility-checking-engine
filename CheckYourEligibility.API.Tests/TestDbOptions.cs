using CheckYourEligibility.API.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CheckYourEligibility.API.Tests;

/// <summary>
///     Single source of in-memory <see cref="EligibilityCheckContext" /> options for unit tests.
///     EF Core caches one internal service provider per distinct options configuration (including each
///     <see cref="InMemoryDatabaseRoot" /> instance and warning configuration). Building options anywhere else
///     in the test project multiplies providers and trips ManyServiceProvidersCreatedWarning.
/// </summary>
public static class TestDbOptions
{
    private static readonly InMemoryDatabaseRoot Root = new();

    public static DbContextOptions<EligibilityCheckContext> Create(string? databaseName = null)
    {
        return CreateBuilder(databaseName).Options;
    }

    // Use when extra options (e.g. interceptors) are needed; note that those create their own provider.
    public static DbContextOptionsBuilder<EligibilityCheckContext> CreateBuilder(string? databaseName = null)
    {
        return new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), Root)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
    }
}
