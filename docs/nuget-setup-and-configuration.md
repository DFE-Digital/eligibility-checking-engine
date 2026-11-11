# NuGet Package Publishing - Setup and Configuration Guide

## Overview

The Core library is automatically published as a NuGet package to the `s203-ece` Azure Artifacts feed. Every commit to `main` triggers an automated pipeline that tests, packs, and publishes the library.

**Key benefit:** Zero manual steps. No more manual versioning, packing, or pushing.

---

## Architecture & Process Flow

### High-Level Flow

```
Developer pushes to main
       ↓
Pipeline triggers (automatic)
       ↓
    ┌──────────────────────────────┐
    │ Stage 1: Build & Test        │
    │ • Build solution             │
    │ • Run Core unit tests        │
    │ • Run Core integration tests │
    │ • Run API tests              │
    └──────────────────────────────┘
       ↓ (only if tests pass)
    ┌──────────────────────────────┐
    │ Stage 2: Pack & Publish      │
    │ • Create NuGet package       │
    │ • Auto-version: 0.1.0.{#}    │
    │ • Publish to s203-ece feed   │
    └──────────────────────────────┘
       ↓
Package available for consumption
```

### Versioning Strategy

**Base Version:** Defined in `.csproj` → `<VersionPrefix>0.1.0</VersionPrefix>`

**Full Version:**
- **Local builds:** `0.1.0-local` (pre-release, never published)
- **Pipeline builds:** `0.1.0.{BUILD_NUMBER}` (e.g., `0.1.0.450`, `0.1.0.451`)

**How it works:**
```
Build #450 → Package 0.1.0.450
Build #451 → Package 0.1.0.451
Build #452 → Package 0.1.0.452
```

**Bumping Major/Minor:**
1. Edit `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`
2. Change: `<VersionPrefix>0.1.0</VersionPrefix>` → `<VersionPrefix>0.2.0</VersionPrefix>`
3. Commit to main
4. Next build: `0.2.0.{BUILD_NUMBER}`

---

## Implementation Details

### Files Modified

#### 1. `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`

```xml
<PropertyGroup>
  <!-- Base version - update for major/minor bumps -->
  <VersionPrefix>0.1.0</VersionPrefix>
  
  <!-- Pipeline builds use build number -->
  <Version Condition="'$(Build_BuildNumber)' != ''">$(VersionPrefix).$(Build_BuildNumber)</Version>
  
  <!-- Local builds use pre-release suffix -->
  <Version Condition="'$(Build_BuildNumber)' == ''">$(VersionPrefix)-local</Version>
</PropertyGroup>
```

**Why this approach:**
- Developers can build locally without setup
- Pipeline has full control over versioning
- `-local` suffix clearly marks pre-release versions
- No manual version edits needed (except base version bumps)

#### 2. `pipeline/publish-nuget.yml`

New file that:
- Triggers on commits to `main` branch
- Runs all tests (unit + integration)
- Fails if tests don't pass (quality gate)
- Packs NuGet package with auto-generated version
- Publishes to `s203-ece` feed

Key section for versioning:
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Pack NuGet package'
  env:
    Build_BuildNumber: $(Build.BuildNumber)  # Pass build # to MSBuild
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
  <PackageReference Include="CheckYourEligibility.Core" Version="0.1.0.456" />
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
```bash
1. Edit code in CheckYourEligibility.Core
2. dotnet test (run locally)
3. git commit && git push origin main
4. Pipeline handles the rest automatically ✓
```

**When bumping version:**
```bash
1. Edit CheckYourEligibility.Core.csproj
   <VersionPrefix>0.2.0</VersionPrefix>
2. git commit && git push origin main
3. Next build: 0.2.0.{BUILD_NUMBER}
```

### Functions App Developers

**Day-to-day (local development):**
```bash
1. Clone both repos side-by-side
2. Open Functions app project
3. dotnet build (Debug = uses project reference)
4. Make changes to Core → instantly available
```

**When updating to new Core version:**
```bash
1. Check Azure Artifacts for latest version
2. Edit Functions app .csproj:
   <PackageReference Include="CheckYourEligibility.Core" Version="0.1.0.456" />
3. dotnet restore && dotnet build -c Release
```

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
- Check feed name is exactly `s203-ece`
- Verify build service account has "Contributor" role
- Check pipeline uses correct feed reference: `publishVstsFeed: 's203-ece'`

**Tests failed, package not published**
- This is correct behavior (quality gate)
- Fix test failures locally
- Commit and push again
- Pipeline will retry automatically

**Package version already exists**
- Build number should auto-increment
- Check Azure DevOps → Pipeline settings → Build number format
- If stuck, manually increment `Build.BuildNumber` counter

---

## Version Examples

| Scenario | Version | Notes |
|----------|---------|-------|
| Local build | `0.1.0-local` | Pre-release, never published |
| Pipeline build #450 | `0.1.0.450` | Official release |
| Pipeline build #451 | `0.1.0.451` | Auto-incremented |
| After bumping base version | `0.2.0.452` | Major/minor version bumped |

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

**I pushed to main but package didn't publish**
- Check pipeline logs (tests may have failed)
- If tests passed but publish failed, check feed permissions

**I need to re-publish an old version**
- Re-push the commit from that version
- Pipeline will create a new build with new version number
- Or use `allowPackageConflicts: true` in pipeline

**I want to test a pre-release version**
- Local builds automatically create `0.1.0-local` packages
- Can be tested with local `nuget.config`
- Won't be pushed to official feed

---

## Key Benefits

✅ **Automated** - No manual steps, no human error  
✅ **Consistent** - Same process every time  
✅ **Safe** - Tests must pass before publish  
✅ **Transparent** - Build numbers in version strings  
✅ **Developer-friendly** - Works with standard `dotnet` commands  
✅ **Scalable** - Works for multiple projects and developers  

---

## Quick Reference

For quick developer reference, see: [nuget-quick-start.md](docs/nuget-quick-start.md)

For implementation checklist, see below.

---

## Implementation Checklist

Use this to ensure everything is properly set up:

### Code Changes
- [x] Updated `CheckYourEligibility.Core.csproj` with dynamic versioning
- [x] Created `pipeline/publish-nuget.yml`
- [x] Local builds work without errors
- [x] Local pack creates `0.1.0-local.nupkg`

### Azure DevOps Setup
- [ ] Pipeline `publish-nuget.yml` added to Azure DevOps
- [ ] Pipeline triggers on commits to `main` (not PRs)
- [ ] Build service has permissions on `s203-ece` feed
- [ ] First test run successful

### Functions App Setup
- [ ] Repos cloned side-by-side
- [ ] `.csproj` updated with conditional references
- [ ] Debug build uses project reference (verified)
- [ ] Release build uses NuGet package (verified)

### Verification
- [ ] Core repo: `dotnet build` works locally
- [ ] Core repo: `dotnet pack -c Release` creates versioned package
- [ ] Functions app: `dotnet build` works in Debug mode
- [ ] Functions app: `dotnet build -c Release` works in Release mode
- [ ] Pipeline: First commit to main triggers automatically
- [ ] Pipeline: Package published to `s203-ece` feed
- [ ] Functions app: Can update package version and build

### Team Communication
- [ ] Share [nuget-quick-start.md](nuget-quick-start.md) with developers
- [ ] Brief team on new process
- [ ] Explain no more manual packing/pushing needed
- [ ] Point to this guide for detailed questions
