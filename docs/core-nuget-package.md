# NuGet Package Publishing

The Core library is published to the `s203-ece` Azure Artifacts feed by the `publish-nuget.yml` pipeline after it runs for a commit on `main`.

The pipeline uses the .NET CLI to pack and publish the package.

## Getting Started

### For Core library developers

Make and test changes on a branch, then open a pull request to `main`. After the pull request is approved and merged, the publish pipeline runs the Core unit and SQL Server integration tests, then publishes the package if they pass. The regular pull-request build pipeline also runs the API tests.

See the [NuGet developer quick start](nuget-quick-start.md).

### For Functions app developers

The Functions app repository can use a project reference for local Debug development and a package reference for Release builds, if configured to do so. See the [setup and configuration guide](nuget-setup-and-configuration.md)
for an example.

## Key Files

| File | Purpose |
|------|---------|
| [CheckYourEligibility.Core.csproj](../CheckYourEligibility.Core/CheckYourEligibility.Core.csproj) | Package ID and version configuration |
| [publish-nuget.yml](../pipeline/publish-nuget.yml) | Build, test, pack and publish pipeline |
| [nuget-quick-start.md](nuget-quick-start.md) | Developer quick reference |
| [nuget-setup-and-configuration.md](nuget-setup-and-configuration.md) | Setup and configuration details |

## Quick Summary

- The package version is based on `VersionPrefix` and the Azure DevOps build ID.
- The publish pipeline must pass its Core unit and integration tests before publishing.
- Use a pull request to merge Core changes to `main`; merging triggers the publish pipeline.
