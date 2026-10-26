using CheckYourEligibility.API.Domain;
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

        var summary = new WorkingFamiliesEventSummary
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
            LatestSubmissionDate = expectedEvent.SubmissionDate,
            LastUpdatedDate = new DateTime(2025, 2, 2)
        };

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
}