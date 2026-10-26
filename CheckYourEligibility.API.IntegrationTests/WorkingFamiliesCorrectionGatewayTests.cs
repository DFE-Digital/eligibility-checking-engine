using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.IntegrationTests;

[TestFixture]
[NonParallelizable]
public class WorkingFamiliesCorrectionGatewayTests
{
    private const string EligibilityCode = "50000375201";

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
}