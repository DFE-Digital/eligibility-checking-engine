# NuGet Publishing - Developer Quick Reference

## For Core Library Developers

### Publishing a Change
```bash
1. Edit CheckYourEligibility.Core code
2. git commit && git push origin main
   ↓
   Pipeline automatically:
   ✓ Runs all tests
   ✓ Creates NuGet package (version: 0.1.0.{BUILD_NUMBER})
   ✓ Publishes to s203-ece feed 
```

### Local Development & Testing
```bash
# Debug mode (default) - instant feedback on code changes
dotnet build
dotnet test

# Release mode - test packaged version (optional)
dotnet build -c Release
```

### Versioning
- **Local:** `0.1.0-local` (for local testing)
- **Pipeline:** `0.1.0.{BUILD_NUMBER}` (e.g., `0.1.0.456`)
- **Bump major/minor:** Edit `.csproj` → Change `<VersionPrefix>0.1.0</VersionPrefix>` to `<VersionPrefix>0.2.0</VersionPrefix>` → Push

---

## For Functions App Developers

### Local Development
```bash
# Clone both repos side-by-side
C:\repos\eligibility-checking-engine
C:\repos\eligibility-checking-engine-functions

# Debug build (default) uses Core project reference
dotnet build
dotnet test
# Changes to Core are instantly available

# Release build uses package from NuGet feed
dotnet build -c Release
```

### Updating to New Core Version
1. Check Azure Artifacts feed for latest version: `0.1.0.{BUILD_NUMBER}`
2. Edit `.csproj` in Functions app:
   ```xml
   <PackageReference Include="CheckYourEligibility.Core" Version="0.1.0.456" />
   ```
3. `dotnet restore` and build

---

## Common Tasks

### I can't build locally
- Ensure you have .NET 10.0 SDK installed
- Run: `dotnet restore`
- If still failing, check: [Troubleshooting](#troubleshooting)

### I want to test the packaged version locally
```bash
# Build and test with Release config to use NuGet package
dotnet build -c Release
dotnet test -c Release
```

### I need to bump the version
Edit `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj`:
```xml
<!-- Before -->
<VersionPrefix>0.1.0</VersionPrefix>

<!-- After -->
<VersionPrefix>0.2.0</VersionPrefix>

<!-- Next build → 0.2.0.{BUILD_NUMBER} -->
```

### Pipeline didn't publish
- Check Azure DevOps → Pipelines → publish-nuget → logs
- Common cause: Tests failed (fix locally and re-push)
- Check build service has permissions on `s203-ece` feed

---
