# NuGet Publishing - Developer Quick Reference

## For Core library developers

### Publish a change

1. Make and test changes on a branch.
2. Open a pull request to `main`.
3. After approval and merge, the `publish-nuget.yml` pipeline runs the Core unit and SQL Server integration tests.
4. If those tests pass, the pipeline packs and publishes the package to the `s203-ece` Azure Artifacts feed.

The regular pull-request build pipeline also runs the API tests.

### Versioning

- **Local package builds:** use the project `VersionPrefix` (currently `0.1.0`);
- **Published packages:** use the version prefix and Azure DevOps build ID, currently `0.1.0.<BuildId>`.
- **Bump the major or minor version:** update `<VersionPrefix>` in `CheckYourEligibility.Core.csproj` and submit the change in a pull request. The next successful publish uses the new prefix and that run's build ID.

## For Functions app developers

The following assumes the Functions app repository is configured to use a local Core project reference in Debug and the published package in Release.

### Local development

Clone both repositories side-by-side, then build and test the Functions app in Debug. Changes to the Core project are then available through the project reference.

Use a Release build to test the package reference configured in the Functions app.

### Update to a new Core package version

1. Find the required package version in the `s203-ece` Azure Artifacts feed.
2. Update the Functions app `.csproj` package reference, for example:

   ```xml
   <PackageReference Include="CheckYourEligibility.Core" Version="0.1.0.12345" />
   ```

3. Restore and build the Functions app.

## Common Tasks

### I can't build locally

- Install the .NET 10 SDK.
- Run `dotnet restore`.
- If the build still fails, see [Troubleshooting in the setup guide](nuget-setup-and-configuration.md#troubleshooting).

### I want to test the packaged version

Build the Functions app in Release mode, provided its project file is configured to reference the package in that configuration:

```bash
dotnet build -c Release
dotnet test -c Release
```

### I need to bump the version

Update `VersionPrefix` in `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`, then submit the change in a pull request. For example:

```xml
<!-- Before -->
<VersionPrefix>0.1.0</VersionPrefix>

<!-- After -->
<VersionPrefix>0.2.0</VersionPrefix>
```

The next successful publish uses `0.2.0.<BuildId>`.

### The publish pipeline did not publish a package

- Check the Azure DevOps pipeline logs for failed Core unit or integration tests.
- If tests passed, check the pipeline identity's permissions on the `s203-ece` feed.
- The publish pipeline runs after changes are merged to `main`, not on pull requests.
