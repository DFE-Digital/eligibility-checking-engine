# Check Your Eligibility API

This repository contains shared business logic and the API for the Eligibility Checking Engine (ECE) service.

## Setup

The projects target .NET 10. Install the .NET 10 SDK to build and run them locally.

### Configuration

Copy [appsettings.json](CheckYourEligibility.API/appsettings.json) to
`CheckYourEligibility.API/appsettings.Development.json` and update the values for your local environment.
This development override is ignored by Git and should not be committed.

For credentials, follow the instructions in the
[eligibility-checking-engine documentation](https://github.com/DFE-Digital/eligibility-checking-engine-documentation),
or ask your lead developer.

### Azure Storage Queue

Queue-backed workflows use Azure Storage Queue and a downstream Logic App. For local development, configure access to the development queue, or avoid workflows that depend on the queue.

## Authentication

Protected API endpoints require a JWT bearer token. Obtain one by posting form-encoded client credentials to
`/oauth2/token`:

```http
POST https://localhost:7117/oauth2/token
Content-Type: application/x-www-form-urlencoded

client_id=<client-id>&client_secret=<client-secret>&scope=<scopes>
```

Send the returned token to protected endpoints using the `Authorization: Bearer <token>` header. Authentication
errors are returned with an HTTP status code and a response body.

## Tests

The repository has NUnit unit tests, SQL Server-backed integration tests, and Cypress API end-to-end tests.

### .NET unit tests

Run these commands from the repository root:

```powershell
dotnet test .\CheckYourEligibility.Core.Tests\CheckYourEligibility.Core.Tests.csproj
dotnet test .\CheckYourEligibility.API.Tests\CheckYourEligibility.API.Tests.csproj
```

### SQL Server integration tests

The integration tests use a disposable SQL Server container. See the
[integration testing guide](docs/integration-testing-guide.md) and
[integration test setup](CheckYourEligibility.Core.IntegrationTests/README.md) for prerequisites and details.

Run them from the repository root:

```powershell
dotnet test .\CheckYourEligibility.Core.IntegrationTests\CheckYourEligibility.Core.IntegrationTests.csproj
```

### Cypress API end-to-end tests

Install Node.js/npm, start the API and its required dependencies, then run these commands from the repository root.
Use credentials configured for your test environment.

```powershell
cd tests
npm install
$env:CYPRESS_API_HOST = 'https://localhost:7117'
$env:CYPRESS_JWT_USERNAME = '<client-id>'
$env:CYPRESS_JWT_PASSWORD = '<client-secret>'
npm run e2e:electron
```

The npm script runs Cypress in Electron. For Bash, set the same environment variables with `export` before running
`npm run e2e:electron`.

### Core NuGet package

See [Core NuGet package details](docs/core-nuget-package.md).

## Ways of working

### Releasing code

Submit pull requests to `main` with functioning code. Reviewers check that the automated tests pass and review the changes. Once approved, the author merges the pull request and deletes the branch.

### Secrets

Do not commit active secrets to this repository. If a secret is committed, notify the appropriate DfE contacts, rewrite
the Git history, and follow DfE processes.

## Swagger

When the application is running, open `/swagger/index.html` to view the API documentation.

## Importing GIAS data

Download the Establishment Fields CSV from [GIAS Downloads](https://get-information-schools.service.gov.uk/Downloads)
by selecting **All Establishment Data** and then **Establishment Fields CSV**.

Upload the CSV as the `file` field in a `multipart/form-data` POST to `/admin/import-establishments`. This endpoint
requires an authenticated token with the admin scope.

## Entity Framework migrations

Run `dotnet ef` commands from the `CheckYourEligibility.API` project directory. The API project is the startup
project; migrations are stored in `CheckYourEligibility.Core`.

### Apply the latest migration

```powershell
dotnet ef database update --project ..\CheckYourEligibility.Core
```

### Add a migration

```powershell
dotnet ef migrations add <migration-name> --project ..\CheckYourEligibility.Core
```

### List migrations

```powershell
dotnet ef migrations list --project ..\CheckYourEligibility.Core
```

### Remove the latest migration

This removes the most recently added migration; the EF CLI does not accept a migration name for this command.

```powershell
dotnet ef migrations remove --project ..\CheckYourEligibility.Core
```

### Apply a specific migration

```powershell
dotnet ef database update <migration-name> --project ..\CheckYourEligibility.Core
```
