# NuGet Package Publishing

The Core library is automatically published as a NuGet package to the `s203-ece` Azure Artifacts feed on every commit to `main`.

## Getting Started

### For Developers (Quick Start)
**See:** [nuget-quick-start.md](nuget-quick-start.md)

**What you need to know:**
- Core devs: Edit code → test locally → `git push main` → Done! (Pipeline handles the rest)
- Functions app devs: Use project reference locally (Debug mode) for instant feedback
- Versioning is automatic: `0.1.0.{BUILD_NUMBER}`

### For Setup & Configuration
**See:** [nuget package setup & config](nuget-package-setup-and-config.md)

**What it covers:**
- Complete architecture and process flow
- Implementation details and technical setup
- Azure DevOps pipeline configuration
- Functions app conditional configuration
- Troubleshooting guide
- Development workflow details

## Key Files

| File | Purpose |
|------|---------|
| `CheckYourEligibility.Core/CheckYourEligibility.Core.csproj` | Auto-versioning configuration |
| `pipeline/publish-nuget.yml` | Automated publish pipeline |
| `docs/nuget-quick-start.md` | Developer quick reference |
| `docs/nuget-package-setup-and-config.md` | Complete setup & configuration guide |

## Quick Summary

✅ **No more manual steps** - Versioning, packing, and publishing are fully automated  
✅ **Safe process** - All tests must pass before publishing (quality gate)  
✅ **Developer-friendly** - Works with standard `dotnet` commands  
✅ **Two modes** - Debug (instant feedback with project reference) + Release (test packaged version)  

## Workflow

```
Core Developers:
  Code → Test locally → git push main → ✓ Done!
  
Functions App Developers:
  Clone both repos → Debug build (instant feedback) → Update version when needed
```
