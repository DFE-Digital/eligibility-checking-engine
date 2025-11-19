# Check Your Eligibility API

This repo contains the API for Eligibility Checking Engine (ECE) and the Check Free School Meals (FSM) service.

## Setup

This is a .NET 8 project - you'll need the latest .NET SDK etc to run it locally.

### Config

When you first clone the repo, you'll want to define your own config. You'll want to copy up the
file [appsettings.json](CheckYourEligibility.API/appsettings.json), name the copy `appsettings.developmnent.json`
in the same folder. Update the values in this new file as needed. This file should not be committed, nor will it with
our .gitignore.

#### Credentials

You can get the credentials through following the instructions
in [check-your-eligibility-infrastructure](https://github.com/DFE-Digital/check-your-eligibility-infrastructure).
Otherwise, just ask your Lead Developer or fellow colleague.

### Queue

There is a Azure Storage Queue part of the whole flow, which triggers a logic app. You can either ignore that part of
the application, mock it, or connect to the dev queue. Credentials in keyvault.

## JWT authenticate your request

Each request needs to be authenticated with a JWT token. YOu get this token bu calling `/api/Login` with the following
object:
`
{
    "username": "",
    "emailAddress": "ecsUi@education.gov.uk",
    "password": ""
}` inserting your username and password.

Any errors will be seen in the Response Headers

Postman collection below automates this.

## How to run tests

We have two test-suites - one .NET NUnit for unit tests and one Cypress for integration and e2e tests. Cypress needs a
running application responding to http calls.

### .NET

VisualStudio does most of this for you, there'll be a button in your UI. Correctly set up Rider too.
`
cd CheckYourEligibility.API.Tests
dotnet test
`

### Cypress

Assuming you have NPM installed.
`
cd tests
npm install
export CYPRESS_API_HOST="https-path to localhost or remote"
export CYPRESS_JWT_USERNAME="JWT user username"
export CYPRESS_JWT_PASSWORD="JWT user password"
npm run e2e:chrome
`

Note, replace `export` with `set` in the above command for Windows.

## Ways of working

### Releasing code

We submit PRs into `main` for functioning code. The reviewer checks that the automated tests pass, then approve.

We expect the code reviewer to run the code locally, read through it and understand what it is trying to solve.

If approved, the original code-creator merges the PR and deletes the branch.

### Secrets

We don't commit active secrets to this repo. If we do, it is crucial to notify DM/TL/PO, rewrite git history and follow
DfE processes.

## Postman scripts

User Swagger doc from below

## Swagger

You'll find a Swagger page on `/swagger/index.html` once you're running the application.

## Getting GIAS data

You need to download a CSV from [GIAS](https://get-information-schools.service.gov.uk/Downloads), selecting All
Establishment Data -> Establishment Fields CSV.

The contents is then POSTed to `/importEstablishments`

## Resources

### Architecture

![Architecture](docs/images/api-infrastructure.png)

### Data flow

![Data flow](docs/images/api-data.png)

### Data structure

![Data structure](docs/images/api-database.png)

### Deployment

![Deployment](docs/images/api-pipeline.png)

### Miscellaneous

#### Migrations

##### Run Latest migration

`dotnet ef update-database`

##### How to add a migration

Add-Migration BaseMigration

##### Update db to latest migration

update-database

##### List Migrations

Get-Migration

Remove-Migration -Force
Remove-Migration

##### Run specific migration

update-database -migration BaseMigration
