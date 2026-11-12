using System.Globalization;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Helpers;

public static class WorkingFamiliesCorrectionDisplayHelper
{
    public static WorkingFamiliesCorrectionRecord SetDisplayProperties(
        WorkingFamiliesCorrectionRecord record,
        DateTime checkDate)
    {
        checkDate = checkDate.Date;

        var childDateOfBirth = record.Child.DateOfBirth.ToString(
            "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var validityStartDate = record.ValidityStartDate.ToString(
            "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var validityEndDate = record.ValidityEndDate.ToString(
            "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var gracePeriodEndDate = record.GracePeriodEndDate?.ToString(
            "yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

        record.EligibilityCodeType =
            WorkingFamiliesCheckHelper.GetEligibilityCodeType(
                record.EligibilityCode);

        record.ChildTooYoung = WorkingFamiliesCheckHelper.ChildIsTooYoung(
            record.Child.DateOfBirth, checkDate);

        record.ChildTooOld = WorkingFamiliesCheckHelper.ChildIsTooOld(
            record.Child.DateOfBirth, checkDate);

        record.TermValidity = WorkingFamiliesCheckHelper.SetTermValidity(
            checkDate,
            gracePeriodEndDate,
            validityStartDate,
            childDateOfBirth,
            record.GracePeriodEndDateApplied);

        var reconfirmation =
            WorkingFamiliesCheckHelper.SetReconfirmationProperties(
                validityEndDate,
                gracePeriodEndDate,
                checkDate,
                record.EligibilityCodeType,
                childDateOfBirth);

        record.ReconfirmationStatus = reconfirmation.Status;

        var hasReconfirmationWindow = reconfirmation.Status is
            ReconfirmationStatus.NotDueYet or
            ReconfirmationStatus.Due or
            ReconfirmationStatus.Overdue;

        record.ReconfirmFrom = hasReconfirmationWindow
            ? reconfirmation.StartDate
            : null;

        record.ReconfirmTo = hasReconfirmationWindow
            ? reconfirmation.EndDate
            : null;

        return record;
    }

    public static WorkingFamiliesCorrectionRecord SetEligibilityConfirmationProperties(
        WorkingFamiliesCorrectionRecord record, IEnumerable<WorkingFamiliesEvent> events)
    {
        // The shared calculation changes the latest event's dates.
        // Use copies so tracked entities and correction versions remain unaffected.
        var eventCopies = events
            .OrderByDescending(item => item.SubmissionDate)
            .Select(item => new WorkingFamiliesEvent
            {
                SubmissionDate = item.SubmissionDate,
                ValidityStartDate = item.ValidityStartDate,
                DiscretionaryValidityStartDate = item.DiscretionaryValidityStartDate,
                ValidityEndDate = item.ValidityEndDate,
                GracePeriodEndDate = item.GracePeriodEndDate,
                ChildDateOfBirth = item.ChildDateOfBirth
            })
            .ToList();

        if (eventCopies.Count == 0)
        {
            throw new ArgumentException("At least one eligibility event is required.", nameof(events));
        }

        var (currentChain, _) =
            WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents(eventCopies);

        record.EligibilityConfirmedOn = currentChain.ValidityStartDate;

        record.IsDiscretionaryValidityStartDateApplied =
            WorkingFamiliesCheckHelper.IsDiscretionaryValidityStartDateApplied(
                currentChain.ValidityStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentChain.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            == true;

        return record;
    }
}