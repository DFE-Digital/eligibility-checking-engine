# NuGet Package Publishing - Setup and Configuration Guide

## Overview

The Core library is published to the `s203-ece` Azure Artifacts feed by the `publish-nuget.yml` pipeline after a change is merged to `main`.

**Key benefit:** Package versioning per pipeline run, packing, and publishing are automated. Update `VersionPrefix` manually when making a major or minor version change.

For a quick developer reference, see [nuget-quick-start.md](nuget-quick-start.md).

---

## Architecture & Process Flow

### High-Level Flow

```
Pull request merged to main
       ↓
Pipeline triggers (automatic)
       ↓
    ┌──────────────────────────────┐
    │ Stage 1: Build & Test        │
    │ • Build solution             │
    │ • Run Core unit tests        │
    │ • Run Core integration tests │
    └──────────────────────────────┘
       ↓ (only if tests pass)
    ┌──────────────────────────────┐
    │ Stage 2: Pack & Publish      │
    │ • Create NuGet package       │
    │ • Version includes BuildId    │
    │ • Publish to s203-ece feed   │
    └──────────────────────────────┘
       ↓
Package available for consumption
```

### Versioning Strategy

**Base Version:** Defined in `.csproj` → `<VersionPrefix>0.1.0</VersionPrefix>`

**Package version:**
- **Local builds:** use the project `VersionPrefix` (currently `0.1.0`);
- **Pipeline builds:** use `VersionPrefix` plus the Azure DevOps build ID (currently `0.1.0.<BuildId>`).

**How it works:**
```
Build ID 450 → Package 0.1.0.450
Build ID 451 → Package 0.1.0.451
Build ID 452 → Package 0.1.0.452
```

**Bumping Major/Minor:**
1. Edit `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`
2. Change: `<VersionPrefix>0.1.0</VersionPrefix>` → `<VersionPrefix>0.2.0</VersionPrefix>`
3. Submit the change in a pull request and merge it to `main`
4. The next successful publish uses `0.2.0.<BuildId>`

---

## Implementation Details

### Relevant files

#### 1. `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`

```xml
<VersionPrefix>0.1.0</VersionPrefix>

<PropertyGroup Condition="'$(Build_BuildId)' != ''">
  <Version>$(VersionPrefix)</Version>
</PropertyGroup>

<PropertyGroup Condition="'$(Build_BuildId)' != ''">
  <PackageVersion>$(VersionPrefix).$(Build_BuildId)</PackageVersion>
</PropertyGroup>
```

**Why this approach:**
- Developers can build locally without setup
- Pipeline has full control over versioning
- Pipeline builds receive a package version containing the Azure DevOps build ID
- No manual version edits needed (except base version bumps)

#### 2. `pipeline/publish-nuget.yml`

This pipeline:
- Triggers on commits to the `main` branch (not pull requests)
- Builds the solution and runs Core unit and SQL Server integration tests
- Fails if tests don't pass (quality gate)
- Packs the NuGet package with the build ID in its version
- Publishes to `s203-ece` feed

The regular pull-request build pipeline separately runs the API tests.

Key section for versioning:
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Pack NuGet package'
  env:
    Build_BuildId: $(Build.BuildId)  # Pass the Azure DevOps build ID to MSBuild
  inputs:
    command: 'pack'
    packagesToPack: 'CheckYourEligibility.Core/CheckYourEligibility.Core.csproj'
    configuration: 'Release'
    packDirectory: '$(Build.ArtifactStagingDirectory)'
    versioningScheme: 'off'  # Let .csproj control version
```

---

## Setup Instructions

### Phase 1: ✅ Code Changes (Already Done)

- ✅ Updated `CheckYourEligibility.Core.csproj` with dynamic versioning
- ✅ Created `pipeline/publish-nuget.yml` 
- ✅ Local builds work without errors

### Phase 2: Configure Azure DevOps Pipeline

1. **Go to Azure DevOps** → Pipelines
2. **Create new pipeline:**
   - Select your repository (eligibility-checking-engine)
   - Choose "Existing Azure Pipelines YAML"
   - Select: `pipeline/publish-nuget.yml`
   - Name it: `publish-nuget`
   - Click "Save and run"

3. **Verify settings:**
   - [ ] Trigger: `main` branch
   - [ ] PR trigger: disabled (`pr: none`)
   - [ ] Build service has "Contributor" role on `s203-ece` feed

### Phase 3: Configure Functions App

In the Azure Functions app repository (`eligibility-checking-engine-functions`):

1. **Update `.csproj` with conditional references:**

```xml
<PropertyGroup>
  <!-- Control which reference to use -->
  <UseLocalCoreProject Condition="'$(Configuration)' == 'Debug'">true</UseLocalCoreProject>
  <UseLocalCoreProject Condition="'$(Configuration)' == 'Release'">false</UseLocalCoreProject>
</PropertyGroup>

<!-- Debug mode: Local project reference (instant feedback) -->
<ItemGroup Condition="$(UseLocalCoreProject) == 'true'">
  <ProjectReference Include="../eligibility-checking-engine/CheckYourEligibility.Core/CheckYourEligibility.Core.csproj" />
</ItemGroup>

<!-- Release mode: NuGet package from feed -->
<ItemGroup Condition="$(UseLocalCoreProject) != 'true'">
  <!-- Replace with a version published to the feed -->
  <PackageReference Include="CheckYourEligibility.Core" Version="0.1.0.12345" />
</ItemGroup>
```

2. **Directory structure (required):**
```
C:\repos\
├── eligibility-checking-engine\
│   └── CheckYourEligibility.Core\
└── eligibility-checking-engine-functions\
    └── (Functions app project)
```

3. **Test locally:**
```bash
# Debug mode (uses project reference)
dotnet build
dotnet test

# Release mode (uses NuGet package)
dotnet build -c Release
```

---

## Development Workflow

### Core Library Developers

**Day-to-day:**

1. Make changes in `CheckYourEligibility.Core` on a branch.
2. Run the Core unit tests and, when appropriate, the SQL Server integration tests.
3. Open a pull request to `main`.
4. After the pull request is approved and merged, the publish pipeline runs and publishes the package if its Core tests pass.

**When bumping version:**
Update `VersionPrefix` in `CheckYourEligibility.Core.csproj` and submit the change in a pull request. After it is merged, the next successful publish uses the new prefix and that run's Azure DevOps build ID.

### Functions App Developers

**Day-to-day (local development):**
```bash
1. Clone both repos side-by-side
2. Open Functions app project
3. dotnet build (Debug = uses project reference)
4. Make changes to Core → instantly available
```

**When updating to new Core version:**

1. Find the required version in the Azure Artifacts feed.
2. Update the Functions app `.csproj` package reference. Replace this example version with the required published version:

   ```xml
   <PackageReference Include="CheckYourEligibility.Core" Version="0.1.0.12345" />
   ```

3. Run `dotnet restore` and build the app in Release mode.

---

## Troubleshooting

### Local Build Errors

**"Project not found" (Functions app)**
- Ensure repos are cloned side-by-side
- Check `.csproj` path: `../eligibility-checking-engine/CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`
- Run: `dotnet clean && dotnet build`

**"Package not found" (Release build)**
- Verify `nuget.config` includes `s203-ece` feed URL
- Check package version exists in feed
- Run: `dotnet nuget list source` to verify feed configuration

**General build failures**
```bash
dotnet clean
dotnet restore
dotnet build -c Release
```

### Pipeline Issues

**Pipeline didn't run**
- Confirmed commit is to `main` branch (not a PR)
- Check Azure DevOps → Pipelines → publish-nuget → Trigger settings
- Verify pipeline YAML has `trigger: - main`

**"Feed not found" error**
- Check the project-scoped feed exists in project `6e58320b-ee65-44e3-9a4b-5f57f27635b4` and is named `s203-ece`
- Check the pipeline uses the project-qualified reference: `publishVstsFeed: '6e58320b-ee65-44e3-9a4b-5f57f27635b4/s203-ece'`
- Verify the pipeline's build service has access to the project and the feed's "Contributor" role

**Core tests failed, package not published**
- This is the publish pipeline's quality gate.
- Fix the failures, submit the changes in a pull request, and merge after approval.
- The publish pipeline runs again for the new commit on `main`.

**Package version already exists**
- The package version uses the Azure DevOps build ID. Check the pipeline logs for the ID used by the run.
- Do not manually change `Build.BuildNumber`; it is not used for package versioning.

---

## Version Examples

| Scenario | Version | Notes |
|----------|---------|-------|
| Local build | `0.1.0` | Uses the project `VersionPrefix`; no local prerelease suffix is configured |
| Pipeline build ID 450 | `0.1.0.450` | Published package |
| Pipeline build ID 451 | `0.1.0.451` | Published package |
| After bumping base version | `0.2.0.<BuildId>` | New prefix with that pipeline run's build ID |

---

## Monitoring & Support

### Check Pipeline Status
- Azure DevOps → Pipelines → publish-nuget
- Each commit to `main` triggers a new run
- Check "Logs" tab for detailed output

### Monitor Published Packages
- Azure DevOps → Artifacts → s203-ece feed
- Search for "CheckYourEligibility.Core"
- View version history and package details

### Common Scenarios

**A change was merged to main but the package wasn't published**
- Check pipeline logs (tests may have failed)
- If tests passed but publish failed, check feed permissions

**I need to publish a change based on an older version**
- Submit the required change in a pull request and merge it to `main`.
- The pipeline publishes a new package version using that run's build ID. Existing package versions are not overwritten.

**I want to test a package locally**
- A local pack uses the project `VersionPrefix` (currently `0.1.0`).
- Local builds are not published to the Azure Artifacts feed by this pipeline.
