using System.Globalization;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

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
}