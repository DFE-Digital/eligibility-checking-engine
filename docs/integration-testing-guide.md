# Integration testing guide

## Purpose

Integration tests cover behaviour that cannot be meaningfully proven through
unit tests, mocks or EF Core's InMemory provider.

They sit between the fast unit-test suite and deployed end-to-end tests. Their
purpose is to exercise the application against isolated instances of its real
technical dependencies.

The repository currently supports SQL Server integration testing through
Testcontainers.

## Choosing the appropriate test layer

| Test layer | Use it to prove |
| --- | --- |
| Unit tests | Business rules, validation, mapping and application logic that can be exercised without real infrastructure |
| Integration tests | Migrations, database constraints, transactions, concurrency, provider-specific queries and other dependency-specific behaviour |
| End-to-end tests | Complete user journeys and interactions with a deployed application and environment |

Integration tests complement unit and end-to-end tests. They should not replace
either layer.

## When to add an integration test

Add an integration test when a requirement depends on behaviour provided by the
real dependency rather than application code alone.

SQL Server examples include:

- competing or concurrent writes;
- unique and foreign-key constraints;
- transactions and rollback;
- row-version and optimistic-concurrency behaviour;
- migrations, schema and seed data;
- SQL Server-specific query translation;
- collation and case or accent sensitivity;
- retry behaviour following database failures.

Keep a scenario in the unit-test project when the behaviour can be proved
accurately without real infrastructure.

## Current repository implementation

The integration-test project is `CheckYourEligibility.API.IntegrationTests`.

It currently:

- starts a disposable SQL Server container;
- reuses one container for the integration-test run;
- applies the application's Entity Framework migrations;
- runs database-sensitive tests against SQL Server;
- removes the container after the run;
- runs separately from the unit-test suite in the pull-request pipeline.

Local setup and execution instructions are documented in
[`CheckYourEligibility.API.IntegrationTests/README.md`](../CheckYourEligibility.API.IntegrationTests/README.md).

## Test design principles

### Use isolated infrastructure

Integration tests must use disposable infrastructure created for the test run.
They must not read from or write to shared DEV, TEST or production databases.

### Reuse expensive dependencies appropriately

Start one SQL Server container for the integration-test run rather than one
container per individual test. Starting a database for every test would make
the suite unnecessarily slow.

### Keep tests independent

A test must not depend on another test having run first.

Create the data required by the scenario and restore any shared mutable test
state that the test changes. Tests should produce the same result regardless of
execution order.

### Use separate database contexts

EF Core `DbContext` instances are not thread-safe.

Each independently executing or concurrent operation must use its own context.
Do not share one context between tasks when testing competing writes.

### Apply real migrations

Build the database using the application's migrations. Do not maintain a
separate hand-written schema for integration tests.

This allows the tests to detect migration, constraint, index and seed-data
problems that InMemory tests cannot reproduce.

### Test observable behaviour

Prefer assertions against durable results, such as:

- persisted records;
- generated values;
- enforced constraints;
- committed or rolled-back changes;
- results returned by competing operations.

Avoid assertions against implementation details unless those details form part
of the required database contract.

## Adding a SQL Server integration scenario

Before adding a test, confirm that the scenario genuinely requires SQL Server.

Then:

1. Add the test to `CheckYourEligibility.API.IntegrationTests`.
2. Reuse the shared SQL Server fixture.
3. Apply or reuse the application migrations.
4. Arrange only the data required by the scenario.
5. Use independent contexts for independent operations.
6. Assert the resulting database or application behaviour.
7. Ensure the test succeeds both alone and as part of the full integration suite.
8. Run the existing unit tests to ensure the fast suite remains unaffected.

## Naming and organisation

Name tests after the externally observable behaviour they prove.

Keep dependency setup in reusable fixtures. Keep scenario-specific data and
assertions in the relevant test class.

Do not move ordinary unit tests into the integration project merely because
database entities are involved. The deciding factor is whether the behaviour
requires the real provider.

## Continuous integration

The pull-request pipeline runs the integration-test project as a separate task.

Keeping it separate:

- makes failures clearly identifiable;
- preserves visibility of the fast unit-test suite;
- allows infrastructure requirements to be managed independently;
- prevents deployment builds from unnecessarily repeating the integration suite.

A new integration dependency must be supported on both approved developer
machines and the CI agent before its tests become mandatory.

## Common mistakes to avoid

Do not:

- use a shared environment database;
- start a new container for every test;
- share a `DbContext` between concurrent tasks;
- depend on test execution order;
- use arbitrary delays to manufacture concurrency;
- duplicate the production schema manually;
- replace useful unit tests with slower integration tests;
- add containerised dependencies without a concrete testing requirement.

## Adding other dependencies

SQL Server is the first supported integration dependency.

Other dependencies, such as Azurite or a Service Bus emulator, should be added
only when a concrete feature requires behaviour that mocks or unit tests cannot
prove accurately.

Follow the same principles:

- isolated disposable infrastructure;
- reusable lifecycle management;
- deterministic independent tests;
- separate CI execution;
- clear local setup documentation.

Do not introduce a large shared framework in anticipation of possible future
requirements. Extract common helpers only after repeated use demonstrates a
stable pattern.

## Reference implementation

Foster-family eligibility-code allocation is the first reference scenario.

It demonstrates why this layer exists: EF Core InMemory could exercise the
business flow but could not prove that concurrent SQL Server writes would
produce unique codes or that the database constraints and migration behaved
correctly.

The SQL-backed tests exposed a concurrency weakness, supported the improved
atomic allocation approach and now protect the complete concurrent
foster-family creation path.