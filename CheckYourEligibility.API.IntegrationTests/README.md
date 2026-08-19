# API integration tests

This project contains database-sensitive integration tests that cannot be
meaningfully executed with EF Core's InMemory provider.

The tests currently use a disposable SQL Server container to verify:

- application migrations and seed data;
- SQL Server row-version and constraint behaviour;
- concurrent eligibility-code allocation;
- concurrent foster-family creation.

## Prerequisites

- .NET 8 SDK
- A Docker-compatible engine running Linux containers

The POC was verified locally using Rancher Desktop with the Moby engine and
Kubernetes disabled. Other approved Docker-compatible engines can also be used.

Verify that the container engine is available before running the tests:

```powershell
docker version
docker run --rm hello-world
```

No connection to a shared development or test database is required.

## Run the integration tests

From the repository root:

```powershell
dotnet test `
    .\CheckYourEligibility.API.IntegrationTests\CheckYourEligibility.API.IntegrationTests.csproj
```

The first run may take longer while the pinned SQL Server image is downloaded.

Testcontainers starts one SQL Server container for the test run, applies the
application migrations, runs the tests, and removes the container afterward.

## Run the existing unit tests

The existing fast unit-test suite remains separate:

```powershell
dotnet test `
    .\CheckYourEligibility.API.Tests\CheckYourEligibility.API.Tests.csproj
```

## Continuous integration

The Azure DevOps PR pipeline runs the integration-test project as a separate
test task on its `ubuntu-latest` hosted agent.

The integration tests do not run as part of deployment builds.

## Troubleshooting

If tests report that they cannot connect to Docker, first run:

```powershell
docker version
docker ps
```

Both commands must be able to contact the container engine.

The SQL Server image used by the tests is pinned in `SqlServerFixture.cs` so
local and CI runs use the same database version.