using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Domain;
using System.Text.Json;
using CheckYourEligibility.API.Helpers;
using FluentAssertions;

namespace CheckYourEligibility.API.Tests.Helpers;

[TestFixture]
public class WorkingFamiliesCorrectionDisplayHelperTests
{
    [TestCase(11, 1, ReconfirmationStatus.NotDueYet)]
    [TestCase(11, 2, ReconfirmationStatus.Due)]
    [TestCase(11, 30, ReconfirmationStatus.Due)]
    [TestCase(12, 1, ReconfirmationStatus.Overdue)]
    public void Reconfirmation_respects_date_boundaries(int month, int day, ReconfirmationStatus expected)
    {
        var record = CreateRecord();

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(
            record, new DateTime(2026, month, day));

        result.EligibilityCodeType.Should().Be(EligibilityCodeType.Standard);
        result.ReconfirmationStatus.Should().Be(expected);
        result.ReconfirmFrom.Should().Be(new DateTime(2026, 11, 2));
        result.ReconfirmTo.Should().Be(new DateTime(2026, 11, 30));
    }

    [Test]
    public void Temporary_code_has_no_reconfirmation_window()
    {
        var record = CreateRecord();
        record.EligibilityCode = "10000349101";

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, new DateTime(2026, 11, 4));

        result.EligibilityCodeType.Should().Be(EligibilityCodeType.Temporary);
        result.ReconfirmationStatus.Should().Be(ReconfirmationStatus.NotApplicable);
        result.ReconfirmFrom.Should().BeNull();
        result.ReconfirmTo.Should().BeNull();
    }

    [Test]
    public void Changing_child_to_school_age_clears_previous_reconfirmation_window()
    {
        var record = CreateRecord();
        var checkDate = new DateTime(2026, 11, 4);

        WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, checkDate);
        record.ReconfirmFrom.Should().NotBeNull();

        record.Child.DateOfBirth = new DateTime(2020, 6, 15);

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, checkDate);

        result.ChildTooOld.Should().BeTrue();
        result.ReconfirmationStatus.Should().Be(ReconfirmationStatus.ChildTooOld);
        result.ReconfirmFrom.Should().BeNull();
        result.ReconfirmTo.Should().BeNull();
        result.TermValidity!.Current!.Name.Should().Be(TermName.None);
        result.TermValidity.Next!.Name.Should().Be(TermName.None);
    }

    [Test]
    public void Young_child_is_flagged_and_has_next_term_validity()
    {
        var record = CreateRecord();
        record.Child.DateOfBirth = new DateTime(2026, 5, 1);

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, new DateTime(2026, 11, 4));

        result.ChildTooYoung.Should().BeTrue();
        result.ChildTooOld.Should().BeFalse();
        result.TermValidity!.Current!.Name.Should().Be(TermName.None);
        result.TermValidity.Next!.Name.Should().Be(TermName.Spring);
        result.TermValidity.Next.StartDate.Should().Be(new DateTime(2027, 1, 1));
    }

    [Test]
    public void Summary_grace_period_can_provide_current_and_next_term_validity()
    {
        var record = CreateRecord();
        record.GracePeriodEndDate = new DateTime(2027, 4, 30);

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, new DateTime(2026, 11, 4));

        result.TermValidity!.Current!.Name.Should().Be(TermName.Autumn);
        result.TermValidity.Next!.Name.Should().Be(TermName.Spring);
        result.TermValidity.Next.StartDate.Should().Be(new DateTime(2027, 1, 1));
    }

    [Test]
    public void Unapplied_grace_period_does_not_produce_valid_terms()
    {
        var record = CreateRecord();
        record.GracePeriodEndDateApplied = false;

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, new DateTime(2026, 11, 4));

        result.TermValidity!.Current!.Name.Should().Be(TermName.None);
        result.TermValidity.Next!.Name.Should().Be(TermName.None);
    }

    [Test]
    public void Missing_grace_period_does_not_expose_default_reconfirmation_dates()
    {
        var record = CreateRecord();
        record.GracePeriodEndDate = null;

        var result = WorkingFamiliesCorrectionDisplayHelper.SetDisplayProperties(record, new DateTime(2026, 11, 4));

        result.ReconfirmationStatus.Should().Be(ReconfirmationStatus.NotApplicable);
        result.ReconfirmFrom.Should().BeNull();
        result.ReconfirmTo.Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Confirmation_uses_current_contiguous_chain_without_changing_source_records(bool breakChain)
    {
        var record = CreateRecord();
        record.Version = "existing-version";
        record.ValidityStartDate = new DateTime(2026, 11, 1);
        record.DiscretionaryValidityStartDate = new DateTime(2026, 11, 1);

        var earliest = new WorkingFamiliesEvent
        {
            SubmissionDate = new DateTime(2026, 1, 10),
            ValidityStartDate = new DateTime(2026, 1, 10),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 9),
            ValidityEndDate = new DateTime(2026, 4, 10),
            GracePeriodEndDate = new DateTime(2026, 8, 31),
            ChildDateOfBirth = record.Child.DateOfBirth
        };

        var middleStartDate = breakChain
            ? new DateTime(2026, 9, 1)
            : new DateTime(2026, 7, 1);

        var middle = new WorkingFamiliesEvent
        {
            SubmissionDate = middleStartDate,
            ValidityStartDate = middleStartDate,
            DiscretionaryValidityStartDate = middleStartDate,
            ValidityEndDate = new DateTime(2026, 10, 1),
            GracePeriodEndDate = new DateTime(2027, 3, 31),
            ChildDateOfBirth = record.Child.DateOfBirth
        };

        var latest = new WorkingFamiliesEvent
        {
            SubmissionDate = new DateTime(2026, 11, 1),
            ValidityStartDate = new DateTime(2026, 11, 1),
            DiscretionaryValidityStartDate = new DateTime(2026, 11, 1),
            ValidityEndDate = new DateTime(2027, 2, 1),
            GracePeriodEndDate = new DateTime(2027, 3, 31),
            ChildDateOfBirth = record.Child.DateOfBirth
        };

        // Deliberately unordered to verify submission-date ordering.
        var events = new[] { latest, earliest, middle };
        var beforeEvents = JsonSerializer.Serialize(events);

        var result = WorkingFamiliesCorrectionDisplayHelper.SetEligibilityConfirmationProperties(record, events);

        var expectedConfirmationDate = breakChain
            ? new DateTime(2026, 9, 1)
            : new DateTime(2026, 1, 10);

        result.EligibilityConfirmedOn.Should().Be(expectedConfirmationDate);
        result.IsDiscretionaryValidityStartDateApplied.Should().Be(!breakChain);

        result.ValidityStartDate.Should().Be(new DateTime(2026, 11, 1));
        result.DiscretionaryValidityStartDate.Should().Be(new DateTime(2026, 11, 1));
        result.Version.Should().Be("existing-version");

        JsonSerializer.Serialize(events).Should().Be(beforeEvents);
    }

    private static WorkingFamiliesCorrectionRecord CreateRecord()
    {
        return new WorkingFamiliesCorrectionRecord
        {
            EligibilityCode = "50000349101",
            SummaryId = "summary-id",
            LatestEventId = "event-id",
            Child = new WorkingFamiliesCorrectionChild
            {
                FirstName = "Sam",
                LastName = "Child",
                DateOfBirth = new DateTime(2023, 6, 15)
            },
            Carer = new WorkingFamiliesCorrectionAdult
            {
                FirstName = "Alex",
                LastName = "Carer"
            },
            ValidityStartDate = new DateTime(2026, 1, 1),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 1),
            ValidityEndDate = new DateTime(2026, 11, 30),
            GracePeriodEndDate = new DateTime(2026, 12, 31),
            GracePeriodEndDateApplied = true
        };
    }
}