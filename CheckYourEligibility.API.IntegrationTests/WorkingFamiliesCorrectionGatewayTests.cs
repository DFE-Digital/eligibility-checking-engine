using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CheckYourEligibility.API.Boundary.Requests;

namespace CheckYourEligibility.API.IntegrationTests;

[TestFixture]
[NonParallelizable]
public class WorkingFamiliesCorrectionGatewayTests
{
    private const string EligibilityCode = "50000375201";

    #region Tests

    [TearDown]
    public async Task RemoveTestRecords()
    {
        await using var context = SqlServerFixture.CreateContext();

        await context.WorkingFamiliesEvents
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ExecuteDeleteAsync();

        await context.WorkingFamiliesEventSummaries
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ExecuteDeleteAsync();
    }

    [Test]
    public async Task Lookup_selects_latest_submission_and_excludes_deleted_events()
    {
        // Arrange: event timestamps deliberately disagree with submission order.
        var olderEvent = CreateEvent("Older", new DateTime(2025, 1, 1));
        olderEvent.EventDateTime = new DateTime(2025, 3, 1);

        var expectedEvent = CreateEvent("Latest", new DateTime(2025, 2, 1));
        expectedEvent.EventDateTime = null;

        var deletedEvent = CreateEvent("Deleted", new DateTime(2025, 4, 1));
        deletedEvent.EventDateTime = new DateTime(2025, 4, 1);
        deletedEvent.IsDeleted = true;
        deletedEvent.DeletedDateTime = new DateTime(2025, 4, 2);

        var summary = CreateSummary();
        summary.LatestSubmissionDate = expectedEvent.SubmissionDate;

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(summary);
            setupContext.WorkingFamiliesEvents.AddRange(
                olderEvent, expectedEvent, deletedEvent);

            await setupContext.SaveChangesAsync();
        }

        // Act: use a fresh context so the lookup reads persisted SQL data.
        await using var lookupContext = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(lookupContext);

        var result = await gateway.GetByEligibilityCode(EligibilityCode);

        // Assert
        result.Should().NotBeNull();
        result!.EligibilityCode.Should().Be(EligibilityCode);
        result.SummaryId.Should().Be(summary.WorkingFamiliesEventSummaryID);
        result.LatestEventId.Should().Be(expectedEvent.WorkingFamiliesEventID);
        result.Child.FirstName.Should().Be("Latest");

        result.ValidityStartDate.Should().Be(summary.ValidityStartDate);
        result.ValidityEndDate.Should().Be(summary.ValidityEndDate);
        result.DiscretionaryValidityStartDate.Should()
            .Be(summary.DiscretionaryValidityStartDate);
        result.GracePeriodEndDate.Should().Be(summary.GracePeriodEndDate);
        result.LastUpdatedDate.Should().Be(summary.LastUpdatedDate);

        result.Partner.Should().BeNull();
        result.HasPartner.Should().BeFalse();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Lookup_rejects_summary_without_usable_event(
    bool includeDeletedEvent)
    {
        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(CreateSummary());

            if (includeDeletedEvent)
            {
                var deletedEvent = CreateEvent(
                    "Deleted", new DateTime(2025, 2, 1));

                deletedEvent.IsDeleted = true;
                deletedEvent.DeletedDateTime = new DateTime(2025, 2, 2);

                setupContext.WorkingFamiliesEvents.Add(deletedEvent);
            }

            await setupContext.SaveChangesAsync();
        }

        await using var lookupContext = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(lookupContext);

        Func<Task> act = () =>
            gateway.GetByEligibilityCode(EligibilityCode);

        await act.Should()
            .ThrowAsync<WorkingFamiliesCorrectionDataException>();
    }

    [Test]
    public async Task Lookup_returns_null_when_no_records_exist()
    {
        await using var context = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(context);

        var result = await gateway.GetByEligibilityCode(EligibilityCode);

        result.Should().BeNull();
    }

    [Test]
    public async Task Lookup_rejects_event_without_summary()
    {
        // Arrange: persist an event, deliberately omitting its summary.
        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEvents.Add(
                CreateEvent("Child", new DateTime(2025, 2, 1)));

            await setupContext.SaveChangesAsync();
        }

        await using var lookupContext = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(lookupContext);

        // Act: capture the operation so we can assert its exception.
        Func<Task> act = () =>
            gateway.GetByEligibilityCode(EligibilityCode);

        // Assert
        await act.Should()
            .ThrowAsync<WorkingFamiliesCorrectionDataException>();
    }

    [Test]
    public async Task Lookup_rejects_duplicate_summaries()
    {
        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.AddRange(
                CreateSummary(),
                CreateSummary());

            setupContext.WorkingFamiliesEvents.Add(
                CreateEvent("Child", new DateTime(2025, 2, 1)));

            await setupContext.SaveChangesAsync();
        }

        await using var lookupContext = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(lookupContext);

        Func<Task> act = () =>
            gateway.GetByEligibilityCode(EligibilityCode);

        await act.Should()
            .ThrowAsync<WorkingFamiliesCorrectionDataException>();
    }

    [Test]
    public async Task Lookup_rejects_events_with_tied_latest_submission_dates()
    {
        var firstEvent = CreateEvent("First", new DateTime(2025, 2, 1));
        firstEvent.EventDateTime = new DateTime(2025, 2, 2);

        var secondEvent = CreateEvent("Second", new DateTime(2025, 2, 1));
        secondEvent.EventDateTime = new DateTime(2025, 2, 3);

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(CreateSummary());
            setupContext.WorkingFamiliesEvents.AddRange(firstEvent, secondEvent);

            await setupContext.SaveChangesAsync();
        }

        await using var lookupContext = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(lookupContext);

        Func<Task> act = () =>
            gateway.GetByEligibilityCode(EligibilityCode);

        await act.Should()
            .ThrowAsync<WorkingFamiliesCorrectionDataException>();
    }

    [Test]
    public async Task Carer_correction_updates_both_records_and_preserves_other_data()
    {
        var olderEvent = CreateEvent("Older", new DateTime(2025, 1, 1));
        var latestEvent = CreateEvent("Latest", new DateTime(2025, 2, 1));

        // Existing partner details must survive a carer correction.
        latestEvent.PartnerFirstName = "Pat";
        latestEvent.PartnerLastName = "Partner";
        latestEvent.PartnerDateOfBirth = new DateTime(1988, 3, 20);
        latestEvent.PartnerNationalInsuranceNumber = "CE123456A";

        var summary = CreateSummary();
        summary.PartnerLastName = "Partner";
        summary.PartnerNationalInsuranceNumber = "CE123456A";

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(summary);
            setupContext.WorkingFamiliesEvents.AddRange(olderEvent, latestEvent);
            await setupContext.SaveChangesAsync();
        }

        await using var updateContext = SqlServerFixture.CreateContext();
        var gateway = new WorkingFamiliesCorrectionGateway(updateContext);

        // Capture persisted snapshots, detached from EF change tracking.
        var beforeSummary = await updateContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var beforeEvents = await updateContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        var beforeOlder = beforeEvents.Single(
            x => x.WorkingFamiliesEventID == olderEvent.WorkingFamiliesEventID);

        var beforeLatest = beforeEvents.Single(
            x => x.WorkingFamiliesEventID == latestEvent.WorkingFamiliesEventID);

        var original = await gateway.GetByEligibilityCode(EligibilityCode);
        original.Should().NotBeNull();

        var request = new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Morgan",
            LastName = "Corrected",
            DateOfBirth = "1987-04-12",
            NationalInsuranceNumber = "ab 65 43 21 d",
            ExpectedVersion = original!.Version
        };

        var result = await gateway.UpdateCarer(EligibilityCode, request);

        result.Should().NotBeNull();
        result!.Carer.FirstName.Should().Be("Morgan");
        result.Carer.LastName.Should().Be("Corrected");
        result.Carer.DateOfBirth.Should().Be(new DateTime(1987, 4, 12));
        result.Carer.NationalInsuranceNumber.Should().Be("AB654321D");
        result.Version.Should().NotBe(original.Version);

        // Verify committed data using a separate context.
        await using var verificationContext = SqlServerFixture.CreateContext();

        var afterSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var afterEvents = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        afterEvents.Should().HaveCount(2);

        var afterOlder = afterEvents.Single(
            x => x.WorkingFamiliesEventID == olderEvent.WorkingFamiliesEventID);

        var afterLatest = afterEvents.Single(
            x => x.WorkingFamiliesEventID == latestEvent.WorkingFamiliesEventID);

        afterOlder.Should().BeEquivalentTo(beforeOlder);

        afterLatest.ParentFirstName.Should().Be("Morgan");
        afterLatest.ParentLastName.Should().Be("Corrected");
        afterLatest.ParentDateOfBirth.Should().Be(new DateTime(1987, 4, 12));
        afterLatest.ParentNationalInsuranceNumber.Should().Be("AB654321D");

        afterLatest.Should().BeEquivalentTo(beforeLatest, options => options
            .Excluding(x => x.ParentFirstName)
            .Excluding(x => x.ParentLastName)
            .Excluding(x => x.ParentDateOfBirth)
            .Excluding(x => x.ParentNationalInsuranceNumber));

        afterSummary.ParentLastName.Should().Be("Corrected");
        afterSummary.ParentNationalInsuranceNumber.Should().Be("AB654321D");
        afterSummary.LastUpdatedDate.Should().BeAfter(beforeSummary.LastUpdatedDate);

        afterSummary.Should().BeEquivalentTo(beforeSummary, options => options
            .Excluding(x => x.ParentLastName)
            .Excluding(x => x.ParentNationalInsuranceNumber)
            .Excluding(x => x.LastUpdatedDate));

        var freshGateway = new WorkingFamiliesCorrectionGateway(verificationContext);
        var reloaded = await freshGateway.GetByEligibilityCode(EligibilityCode);

        reloaded.Should().NotBeNull();
        result.Version.Should().Be(reloaded!.Version);
    }

    [TestCase("summary")]
    [TestCase("event")]
    [TestCase("new-latest-event")]
    public async Task Carer_correction_rejects_stale_version_without_changing_records(
    string changedRecord)
    {
        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(CreateSummary());
            setupContext.WorkingFamiliesEvents.Add(
                CreateEvent("Latest", new DateTime(2025, 2, 1)));

            await setupContext.SaveChangesAsync();
        }

        // Simulate the version obtained when the user opened the record.
        string originalVersion;

        await using (var lookupContext = SqlServerFixture.CreateContext())
        {
            var gateway = new WorkingFamiliesCorrectionGateway(lookupContext);
            var original = await gateway.GetByEligibilityCode(EligibilityCode);

            original.Should().NotBeNull();
            originalVersion = original!.Version;
        }

        // Simulate another writer changing data after the screen was opened.
        await using (var otherContext = SqlServerFixture.CreateContext())
        {
            switch (changedRecord)
            {
                case "summary":
                    var summary = await otherContext.WorkingFamiliesEventSummaries
                        .SingleAsync(x => x.EligibilityCode == EligibilityCode);

                    summary.ChildPostCode = "M1 1AE";
                    break;

                case "event":
                    var existingEvent = await otherContext.WorkingFamiliesEvents
                        .SingleAsync(x => x.EligibilityCode == EligibilityCode);

                    existingEvent.ParentDateOfBirth = new DateTime(1986, 6, 11);
                    break;

                case "new-latest-event":
                    otherContext.WorkingFamiliesEvents.Add(
                        CreateEvent("New latest", new DateTime(2025, 3, 1)));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(changedRecord));
            }

            await otherContext.SaveChangesAsync();
        }

        // Snapshot the other writer's committed changes.
        await using var verificationContext = SqlServerFixture.CreateContext();

        var beforeSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var beforeEvents = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        var request = new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Morgan",
            LastName = "ShouldNotBeSaved",
            DateOfBirth = "1987-04-12",
            NationalInsuranceNumber = "AB654321D",
            ExpectedVersion = originalVersion
        };

        await using (var updateContext = SqlServerFixture.CreateContext())
        {
            var gateway = new WorkingFamiliesCorrectionGateway(updateContext);

            Func<Task> act = () =>
                gateway.UpdateCarer(EligibilityCode, request);

            var exception = await act.Should()
                .ThrowAsync<WorkingFamiliesCorrectionConflictException>();

            exception.Which.Message.Should().Be(
                "An error occurred - a record update conflict was detected - please try again");
        }

        var afterSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var afterEvents = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        afterSummary.Should().BeEquivalentTo(beforeSummary);
        afterEvents.Should().BeEquivalentTo(beforeEvents);
    }

    [Test]
    public async Task Carer_correction_rolls_back_both_records_when_failure_occurs_after_writes()
    {
        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(CreateSummary());
            setupContext.WorkingFamiliesEvents.Add(
                CreateEvent("Latest", new DateTime(2025, 2, 1)));

            await setupContext.SaveChangesAsync();
        }

        await using var verificationContext = SqlServerFixture.CreateContext();

        var beforeSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var beforeEvent = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var lookupGateway =
            new WorkingFamiliesCorrectionGateway(verificationContext);

        var original = await lookupGateway.GetByEligibilityCode(EligibilityCode);
        original.Should().NotBeNull();

        var request = new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Morgan",
            LastName = "Corrected",
            DateOfBirth = "1987-04-12",
            NationalInsuranceNumber = "AB654321D",
            ExpectedVersion = original!.Version
        };

        var interceptor = new FailAfterCorrectionWritesInterceptor();

        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseSqlServer(SqlServerFixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using (var updateContext = new EligibilityCheckContext(options))
        {
            var gateway = new WorkingFamiliesCorrectionGateway(updateContext);

            Func<Task> act = () =>
                gateway.UpdateCarer(EligibilityCode, request);

            var exception = await act.Should()
                .ThrowAsync<InvalidOperationException>();

            exception.Which.Message.Should()
                .Be(FailAfterCorrectionWritesInterceptor.FailureMessage);
        }

        // Prove both updates reached SQL before the injected failure.
        interceptor.SummaryLastNameSeen.Should().Be("Corrected");
        interceptor.EventLastNameSeen.Should().Be("Corrected");

        // Outside the failed transaction, both records must be unchanged.
        var afterSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var afterEvent = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        afterSummary.Should().BeEquivalentTo(beforeSummary);
        afterEvent.Should().BeEquivalentTo(beforeEvent);
    }

    private sealed class FailAfterCorrectionWritesInterceptor
    : SaveChangesInterceptor
    {
        public const string FailureMessage =
            "Simulated failure after SQL writes.";

        public string? SummaryLastNameSeen { get; private set; }

        public string? EventLastNameSeen { get; private set; }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context
                ?? throw new InvalidOperationException(
                    "The interceptor requires a database context.");

            SummaryLastNameSeen = await context
                .Set<WorkingFamiliesEventSummary>()
                .AsNoTracking()
                .Where(x => x.EligibilityCode == EligibilityCode)
                .Select(x => x.ParentLastName)
                .SingleAsync(cancellationToken);

            EventLastNameSeen = await context
                .Set<WorkingFamiliesEvent>()
                .AsNoTracking()
                .Where(x => x.EligibilityCode == EligibilityCode)
                .Select(x => x.ParentLastName)
                .SingleAsync(cancellationToken);

            throw new InvalidOperationException(FailureMessage);
        }
    }

    [TestCase(26)]
    [TestCase(27)]
    [TestCase(100)]
    public async Task Child_correction_persists_long_names_and_preserves_other_data(
        int nameLength)
    {
        var olderEvent = CreateEvent("Older", new DateTime(2025, 1, 1));
        var latestEvent = CreateEvent("Latest", new DateTime(2025, 2, 1));

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(CreateSummary());
            setupContext.WorkingFamiliesEvents.AddRange(olderEvent, latestEvent);
            await setupContext.SaveChangesAsync();
        }

        await using var updateContext = SqlServerFixture.CreateContext();

        var beforeSummary = await updateContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var beforeEvents = await updateContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        var beforeOlder = beforeEvents.Single(
            x => x.WorkingFamiliesEventID == olderEvent.WorkingFamiliesEventID);

        var beforeLatest = beforeEvents.Single(
            x => x.WorkingFamiliesEventID == latestEvent.WorkingFamiliesEventID);

        var gateway = new WorkingFamiliesCorrectionGateway(updateContext);
        var original = await gateway.GetByEligibilityCode(EligibilityCode);

        original.Should().NotBeNull();

        var firstName = new string('A', nameLength);

        var request = new WorkingFamiliesChildCorrectionRequest
        {
            FirstName = firstName,
            LastName = "CorrectedChild",
            DateOfBirth = "2000-04-12",
            Postcode = "M1 1AE",
            ExpectedVersion = original!.Version
        };

        var result = await gateway.UpdateChild(EligibilityCode, request);

        result.Should().NotBeNull();
        result!.Child.FirstName.Should().Be(firstName);
        result.Child.LastName.Should().Be("CorrectedChild");

        await using var verificationContext = SqlServerFixture.CreateContext();

        var afterSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var afterEvents = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        afterEvents.Should().HaveCount(2);

        var afterOlder = afterEvents.Single(
            x => x.WorkingFamiliesEventID == olderEvent.WorkingFamiliesEventID);

        var afterLatest = afterEvents.Single(
            x => x.WorkingFamiliesEventID == latestEvent.WorkingFamiliesEventID);

        afterOlder.Should().BeEquivalentTo(beforeOlder);

        afterLatest.ChildFirstName.Should().Be(firstName);
        afterLatest.ChildLastName.Should().Be("CorrectedChild");
        afterLatest.ChildDateOfBirth.Should().Be(new DateTime(2000, 4, 12));
        afterLatest.ChildPostCode.Should().Be("M1 1AE");

        afterLatest.Should().BeEquivalentTo(beforeLatest, options => options
            .Excluding(x => x.ChildFirstName)
            .Excluding(x => x.ChildLastName)
            .Excluding(x => x.ChildDateOfBirth)
            .Excluding(x => x.ChildPostCode));

        afterSummary.ChildFirstName.Should().Be(firstName);
        afterSummary.ChildFirstNameTruncated.Should()
            .Be(new string('a', nameLength));
        afterSummary.ChildDateOfBirth.Should().Be(new DateTime(2000, 4, 12));
        afterSummary.ChildPostCode.Should().Be("M1 1AE");
        afterSummary.LastUpdatedDate.Should().BeAfter(beforeSummary.LastUpdatedDate);

        afterSummary.Should().BeEquivalentTo(beforeSummary, options => options
            .Excluding(x => x.ChildFirstName)
            .Excluding(x => x.ChildFirstNameTruncated)
            .Excluding(x => x.ChildDateOfBirth)
            .Excluding(x => x.ChildPostCode)
            .Excluding(x => x.LastUpdatedDate));
    }

    [Test]
    public async Task Child_correction_trims_leading_whitespace_when_deriving_summary_name()
    {
        var summary = CreateSummary();
        var latestEvent = CreateEvent(
            "Latest",
            new DateTime(2025, 2, 1));

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(summary);
            setupContext.WorkingFamiliesEvents.Add(latestEvent);
            await setupContext.SaveChangesAsync();
        }

        string expectedVersion;

        await using (var lookupContext = SqlServerFixture.CreateContext())
        {
            var lookupGateway =
                new WorkingFamiliesCorrectionGateway(lookupContext);

            var record =
                await lookupGateway.GetByEligibilityCode(EligibilityCode);

            record.Should().NotBeNull();
            expectedVersion = record!.Version;
        }

        var request = new WorkingFamiliesChildCorrectionRequest
        {
            FirstName = " Sam",
            LastName = "CorrectedChild",
            DateOfBirth = "2000-04-12",
            Postcode = "M1 1AE",
            ExpectedVersion = expectedVersion
        };

        await using (var updateContext = SqlServerFixture.CreateContext())
        {
            var gateway = new WorkingFamiliesCorrectionGateway(updateContext);
            var result = await gateway.UpdateChild(EligibilityCode, request);

            result.Should().NotBeNull();
            result!.Child.FirstName.Should().Be(" Sam");
        }

        await using var verificationContext = SqlServerFixture.CreateContext();

        var persistedSummary =
            await verificationContext.WorkingFamiliesEventSummaries
                .AsNoTracking()
                .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var persistedEvent =
            await verificationContext.WorkingFamiliesEvents
                .AsNoTracking()
                .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        persistedSummary.ChildFirstName.Should().Be(" Sam");
        persistedSummary.ChildFirstNameTruncated.Should().Be("sam");
        persistedEvent.ChildFirstName.Should().Be(" Sam");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Partner_correction_updates_existing_partner_and_preserves_other_data(
        bool partialPartner)
    {
        var olderEvent = CreateEvent("Older", new DateTime(2025, 1, 1));
        var latestEvent = CreateEvent("Latest", new DateTime(2025, 2, 1));

        latestEvent.PartnerFirstName = "Pat";

        if (!partialPartner)
        {
            latestEvent.PartnerLastName = "Partner";
            latestEvent.PartnerDateOfBirth = new DateTime(1988, 3, 20);
            latestEvent.PartnerNationalInsuranceNumber = "CE123456A";
        }

        var summary = CreateSummary();
        summary.PartnerLastName = latestEvent.PartnerLastName;
        summary.PartnerNationalInsuranceNumber =
            latestEvent.PartnerNationalInsuranceNumber;

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(summary);
            setupContext.WorkingFamiliesEvents.AddRange(olderEvent, latestEvent);
            await setupContext.SaveChangesAsync();
        }

        await using var verificationContext = SqlServerFixture.CreateContext();

        var beforeSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var beforeEvents = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        var beforeOlder = beforeEvents.Single(
            x => x.WorkingFamiliesEventID == olderEvent.WorkingFamiliesEventID);

        var beforeLatest = beforeEvents.Single(
            x => x.WorkingFamiliesEventID == latestEvent.WorkingFamiliesEventID);

        var lookupGateway =
            new WorkingFamiliesCorrectionGateway(verificationContext);

        var original = await lookupGateway.GetByEligibilityCode(EligibilityCode);
        original.Should().NotBeNull();
        original!.HasPartner.Should().BeTrue();

        var request = new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Morgan",
            LastName = "CorrectedPartner",
            DateOfBirth = "1990-04-12",
            NationalInsuranceNumber = "ce 65 43 21 b",
            ExpectedVersion = original.Version
        };

        await using (var updateContext = SqlServerFixture.CreateContext())
        {
            var gateway = new WorkingFamiliesCorrectionGateway(updateContext);
            var result = await gateway.UpdatePartner(EligibilityCode, request);

            result.Should().NotBeNull();
            result!.HasPartner.Should().BeTrue();
            result.Partner.Should().NotBeNull();
            result.Partner!.FirstName.Should().Be("Morgan");
            result.Partner.LastName.Should().Be("CorrectedPartner");
            result.Partner.DateOfBirth.Should().Be(new DateTime(1990, 4, 12));
            result.Partner.NationalInsuranceNumber.Should().Be("CE654321B");
        }

        var afterSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var afterEvents = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == EligibilityCode)
            .ToListAsync();

        afterEvents.Should().HaveCount(2);

        var afterOlder = afterEvents.Single(
            x => x.WorkingFamiliesEventID == olderEvent.WorkingFamiliesEventID);

        var afterLatest = afterEvents.Single(
            x => x.WorkingFamiliesEventID == latestEvent.WorkingFamiliesEventID);

        afterOlder.Should().BeEquivalentTo(beforeOlder);

        afterLatest.PartnerFirstName.Should().Be("Morgan");
        afterLatest.PartnerLastName.Should().Be("CorrectedPartner");
        afterLatest.PartnerDateOfBirth.Should().Be(new DateTime(1990, 4, 12));
        afterLatest.PartnerNationalInsuranceNumber.Should().Be("CE654321B");

        afterLatest.Should().BeEquivalentTo(beforeLatest, options => options
            .Excluding(x => x.PartnerFirstName)
            .Excluding(x => x.PartnerLastName)
            .Excluding(x => x.PartnerDateOfBirth)
            .Excluding(x => x.PartnerNationalInsuranceNumber));

        afterSummary.PartnerLastName.Should().Be("CorrectedPartner");
        afterSummary.PartnerNationalInsuranceNumber.Should().Be("CE654321B");
        afterSummary.LastUpdatedDate.Should().BeAfter(beforeSummary.LastUpdatedDate);

        afterSummary.Should().BeEquivalentTo(beforeSummary, options => options
            .Excluding(x => x.PartnerLastName)
            .Excluding(x => x.PartnerNationalInsuranceNumber)
            .Excluding(x => x.LastUpdatedDate));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase(null, true)]
    public async Task Partner_correction_cannot_add_a_partner(
        string? emptyValue,
        bool useDefaultDate)
    {
        var latestEvent = CreateEvent("Latest", new DateTime(2025, 2, 1));

        latestEvent.PartnerFirstName = emptyValue;
        latestEvent.PartnerLastName = emptyValue;
        latestEvent.PartnerNationalInsuranceNumber = emptyValue;
        latestEvent.PartnerDateOfBirth =
            useDefaultDate ? DateTime.MinValue : null;

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(CreateSummary());
            setupContext.WorkingFamiliesEvents.Add(latestEvent);
            await setupContext.SaveChangesAsync();
        }

        await using var verificationContext = SqlServerFixture.CreateContext();

        var beforeSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var beforeEvent = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var lookupGateway =
            new WorkingFamiliesCorrectionGateway(verificationContext);

        var original = await lookupGateway.GetByEligibilityCode(EligibilityCode);

        original.Should().NotBeNull();
        original!.HasPartner.Should().BeFalse();
        original.Partner.Should().BeNull();

        var request = new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Morgan",
            LastName = "MustNotBeAdded",
            DateOfBirth = "1990-04-12",
            NationalInsuranceNumber = "CE654321B",
            ExpectedVersion = original.Version
        };

        await using (var updateContext = SqlServerFixture.CreateContext())
        {
            var gateway = new WorkingFamiliesCorrectionGateway(updateContext);

            Func<Task> act = () =>
                gateway.UpdatePartner(EligibilityCode, request);

            await act.Should()
                .ThrowAsync<WorkingFamiliesCorrectionPartnerMissingException>();
        }

        var afterSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var afterEvent = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        afterSummary.Should().BeEquivalentTo(beforeSummary);
        afterEvent.Should().BeEquivalentTo(beforeEvent);
    }

    [TestCase("child")]
    [TestCase("carer")]
    [TestCase("partner")]
    public async Task Corrections_preserve_supported_accented_names_at_maximum_length(
        string section)
    {
        var latestEvent = CreateEvent("Latest", new DateTime(2025, 2, 1));

        latestEvent.PartnerFirstName = "Pat";
        latestEvent.PartnerLastName = "Partner";
        latestEvent.PartnerDateOfBirth = new DateTime(1988, 3, 20);
        latestEvent.PartnerNationalInsuranceNumber = "CE123456A";

        var summary = CreateSummary();
        summary.PartnerLastName = "Partner";
        summary.PartnerNationalInsuranceNumber = "CE123456A";

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            setupContext.WorkingFamiliesEventSummaries.Add(summary);
            setupContext.WorkingFamiliesEvents.Add(latestEvent);
            await setupContext.SaveChangesAsync();
        }

        // Both characters are included in the shared name-validation rules.
        var firstName = new string('Ŵ', 100);
        var lastName = new string('Ć', 100);

        await using (var updateContext = SqlServerFixture.CreateContext())
        {
            var gateway = new WorkingFamiliesCorrectionGateway(updateContext);
            var original = await gateway.GetByEligibilityCode(EligibilityCode);

            original.Should().NotBeNull();

            if (section == "child")
            {
                await gateway.UpdateChild(
                    EligibilityCode,
                    new WorkingFamiliesChildCorrectionRequest
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        DateOfBirth = "2021-06-15",
                        Postcode = "SW1A 1AA",
                        ExpectedVersion = original!.Version
                    });
            }
            else
            {
                var request = new WorkingFamiliesAdultCorrectionRequest
                {
                    FirstName = firstName,
                    LastName = lastName,
                    DateOfBirth = "1985-05-10",
                    NationalInsuranceNumber = "AB654321D",
                    ExpectedVersion = original!.Version
                };

                if (section == "carer")
                {
                    await gateway.UpdateCarer(EligibilityCode, request);
                }
                else
                {
                    await gateway.UpdatePartner(EligibilityCode, request);
                }
            }
        }

        await using var verificationContext = SqlServerFixture.CreateContext();

        var savedEvent = await verificationContext.WorkingFamiliesEvents
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        var actualNames = section switch
        {
            "child" => (
                savedEvent.ChildFirstName,
                savedEvent.ChildLastName),

            "carer" => (
                savedEvent.ParentFirstName,
                savedEvent.ParentLastName),

            "partner" => (
                savedEvent.PartnerFirstName,
                savedEvent.PartnerLastName),

            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };

        actualNames.Item1.Should().Be(firstName);
        actualNames.Item2.Should().Be(lastName);

        var savedSummary = await verificationContext.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == EligibilityCode);

        switch (section)
        {
            case "child":
                savedSummary.ChildFirstName.Should().Be(firstName);
                savedSummary.ChildFirstNameTruncated.Should()
                    .Be(new string('ŵ', 100));
                break;

            case "carer":
                savedSummary.ParentLastName.Should().Be(lastName);
                break;

            case "partner":
                savedSummary.PartnerLastName.Should().Be(lastName);
                break;
        }
    }

    #endregion

    #region Helpers
    private static WorkingFamiliesEvent CreateEvent(
        string childFirstName,
        DateTime submissionDate)
    {
        return new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            EligibilityCode = EligibilityCode,
            ChildFirstName = childFirstName,
            ChildLastName = "Child",
            ChildDateOfBirth = new DateTime(2021, 6, 15),
            ChildPostCode = "SW1A 1AA",
            ParentFirstName = "Alex",
            ParentLastName = "Carer",
            ParentDateOfBirth = new DateTime(1985, 5, 10),
            ParentNationalInsuranceNumber = "AB123456C",
            SubmissionDate = submissionDate,
            ValidityStartDate = new DateTime(2025, 1, 1),
            ValidityEndDate = new DateTime(2025, 3, 31),
            DiscretionaryValidityStartDate = new DateTime(2025, 1, 1),
            GracePeriodEndDate = new DateTime(2025, 8, 31)
        };
    }

    private static WorkingFamiliesEventSummary CreateSummary()
    {
        return new WorkingFamiliesEventSummary
        {
            WorkingFamiliesEventSummaryID = Guid.NewGuid().ToString(),
            EligibilityCode = EligibilityCode,
            ChildFirstName = "Summary",
            ChildFirstNameTruncated = "summary",
            ChildDateOfBirth = new DateTime(2021, 6, 15),
            ChildPostCode = "SW1A 1AA",
            ParentNationalInsuranceNumber = "AB123456C",
            ParentLastName = "Carer",
            ValidityStartDate = new DateTime(2025, 1, 1),
            ValidityEndDate = new DateTime(2025, 3, 31),
            DiscretionaryValidityStartDate = new DateTime(2025, 1, 1),
            GracePeriodEndDate = new DateTime(2025, 8, 31),
            LatestSubmissionDate = new DateTime(2025, 2, 1),
            LastUpdatedDate = new DateTime(2025, 2, 2)
        };
    }
    #endregion
}