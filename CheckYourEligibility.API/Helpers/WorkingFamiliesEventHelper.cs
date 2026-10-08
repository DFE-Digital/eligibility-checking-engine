using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Helpers;


public static class WorkingFamiliesEventHelper
{

    public static WorkingFamiliesEvent ParseWorkingFamilyEventFromFosterFamily(
        FosterCarer fosterCarer,
        FosterChild fosterChild,
        string eligibilityCode,
        DateTime validityStartDate,
        DateTime submissionDate)
    {
        WorkingFamiliesEvent wfEvent = new()
        {
            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            EligibilityCode = eligibilityCode,
            ValidityStartDate = validityStartDate,
            ValidityEndDate = validityStartDate.AddMonths(3),

            ParentNationalInsuranceNumber = fosterCarer.NationalInsuranceNumber?.ToUpper().Replace(" ", string.Empty),
            ParentFirstName = fosterCarer.FirstName,
            ParentLastName = fosterCarer.LastName,
            ParentDateOfBirth = fosterCarer.DateOfBirth,
            PartnerNationalInsuranceNumber = fosterCarer.PartnerNationalInsuranceNumber?.ToUpper().Replace(" ", string.Empty) ?? string.Empty,
            PartnerFirstName = fosterCarer.PartnerFirstName ?? string.Empty,
            PartnerLastName = fosterCarer.PartnerLastName ?? string.Empty,
            PartnerDateOfBirth = fosterCarer.PartnerDateOfBirth,

            ChildFirstName = fosterChild.FirstName,
            ChildLastName = fosterChild.LastName,
            ChildPostCode = fosterChild.PostCode,
            ChildDateOfBirth = fosterChild.DateOfBirth,
            SubmissionDate = submissionDate,

            DiscretionaryValidityStartDate = GetDiscretionaryStartDate(validityStartDate, submissionDate), // validity start date and submmission
            GracePeriodEndDate = GetGracePeriodEndDate(validityStartDate.AddMonths(3)),

            CreatedDateTime = DateTime.UtcNow,
            EventDateTime = DateTime.UtcNow
        };

        return wfEvent;
    }
    /// <summary>
    /// Maps dates from the newest event to a summary record.
    /// </summary>
    /// <param name="workingFamiliesEvent"></param>
    /// <param name="isContiguous">
    /// if True - it will also map FirstEventDate, LastCheckDate, FirstCheckDate, DVSD, VSD</param>
    /// <returns></returns>
    public static WorkingFamiliesEventSummary MapWorkingFamiliesEventUpdateDatesToSummaryRecord(WorkingFamiliesEvent workingFamiliesEvent, WorkingFamiliesEventSummary eventSummary, bool isContiguous = false)
    {

        DateTime today = DateTime.UtcNow.Date;

        eventSummary.LastUpdatedDate = today;
        eventSummary.LatestSubmissionDate = workingFamiliesEvent.SubmissionDate;
        eventSummary.ValidityEndDate = workingFamiliesEvent.ValidityEndDate;
        eventSummary.GracePeriodEndDate = workingFamiliesEvent.GracePeriodEndDate;
        eventSummary.GracePeriodEndDateApplied = true;

        if (!isContiguous)
        {
            eventSummary.FirstEventDate = today;
            eventSummary.DiscretionaryValidityStartDate = workingFamiliesEvent.DiscretionaryValidityStartDate;
            eventSummary.ValidityStartDate = workingFamiliesEvent.ValidityStartDate;

        }
        return eventSummary;
    }
    public static WorkingFamiliesEventSummary MapWorkingFamiliesEventToNewSummaryRecord(WorkingFamiliesEvent workingFamiliesEvent)
    {

        DateTime today = DateTime.UtcNow.Date;
        var currentTerm = WorkingFamiliesCheckHelper.GetTerms(today).Current;
        WorkingFamiliesEventSummary newEventSummary = new WorkingFamiliesEventSummary()
        {
            WorkingFamiliesEventSummaryID = Guid.NewGuid().ToString(),
            EligibilityCode = workingFamiliesEvent.EligibilityCode,
            ChildDateOfBirth = workingFamiliesEvent.ChildDateOfBirth,
            ChildFirstName = workingFamiliesEvent.ChildFirstName,
            ParentLastName = workingFamiliesEvent.ParentLastName,
            PartnerLastName = workingFamiliesEvent.PartnerLastName,
            ParentNationalInsuranceNumber = workingFamiliesEvent.ParentNationalInsuranceNumber ?? string.Empty, // why do we allow null for the event but not for the summary ? ,
            PartnerNationalInsuranceNumber = workingFamiliesEvent.PartnerNationalInsuranceNumber,
            ChildPostCode = workingFamiliesEvent.ChildPostCode ?? string.Empty, // why do we allow null for the event but not for the summary ?          
            ChildFirstNameTruncated = workingFamiliesEvent.ChildFirstName.Replace("-", " ").Split(" ").First().ToLower().Trim(),
            FirstEventDate = today,
            HasCodeBeenCheckedByOwningLA = false,
            LastUpdatedDate = today,
            LatestSubmissionDate = workingFamiliesEvent.SubmissionDate,
            OwningLocalAuthorityId = null, //how do we get this information ?
            GracePeriodEndDate = workingFamiliesEvent.GracePeriodEndDate,
            DiscretionaryValidityStartDate = workingFamiliesEvent.DiscretionaryValidityStartDate,
            ValidityStartDate = workingFamiliesEvent.ValidityStartDate,
            ValidityEndDate = workingFamiliesEvent.ValidityEndDate,
            GracePeriodEndDateApplied = WorkingFamiliesCheckHelper.isGracePeriodEndDateApplied(workingFamiliesEvent.DiscretionaryValidityStartDate, workingFamiliesEvent.ValidityEndDate, 1)
        };
        return newEventSummary;

    }

    /// <summary>
    /// Map personal data to the summary record from its event record
    /// </summary>
    /// <param name="workingFamiliesEvent"></param>
    /// <returns></returns>
    public static WorkingFamiliesEventSummary MapPIWorkingFamilySummaryFromWorkingFamilyEvent(WorkingFamiliesEventSummary eventSummary, WorkingFamiliesEvent workingFamiliesEvent)
    {

        eventSummary.ChildDateOfBirth = workingFamiliesEvent.ChildDateOfBirth;
        eventSummary.ParentNationalInsuranceNumber = workingFamiliesEvent.ParentNationalInsuranceNumber;
        eventSummary.PartnerNationalInsuranceNumber = workingFamiliesEvent.PartnerNationalInsuranceNumber;
        eventSummary.ChildPostCode = workingFamiliesEvent.ChildPostCode ?? string.Empty;
        eventSummary.ChildFirstName = workingFamiliesEvent.ChildFirstName;
        eventSummary.ParentLastName = workingFamiliesEvent.ParentLastName;
        eventSummary.PartnerLastName = workingFamiliesEvent.PartnerLastName;
        eventSummary.ChildFirstNameTruncated = workingFamiliesEvent.ChildFirstName.Replace("-", " ").Split(" ").First().ToLower().Trim();

        return eventSummary;
    }

    public static WorkingFamiliesEvent ParseWorkingFamiliesEvent(List<string> eventProps, List<string> columnHeaders)
    {
        var validityStartDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Validity Start Date")]));
        var validityEndDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Validity End Date")]));
        var submissionDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Submission Date")]));
        DateTime? partnerDateOfBirth = !string.IsNullOrEmpty(eventProps[columnHeaders.IndexOf("Partner DOB")]) ? DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Partner DOB")])) : null;
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            EligibilityCode = eventProps[columnHeaders.IndexOf("Eligibility Code")],
            ValidityStartDate = validityStartDate,
            ValidityEndDate = validityEndDate,
            ParentNationalInsuranceNumber = eventProps[columnHeaders.IndexOf("Parent NINO")],
            ParentFirstName = eventProps[columnHeaders.IndexOf("Parent Forename")],
            ParentLastName = eventProps[columnHeaders.IndexOf("Parent Surname")],
            ParentDateOfBirth = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Parent DOB")])),
            ChildFirstName = eventProps[columnHeaders.IndexOf("Child Forename")],
            ChildLastName = eventProps[columnHeaders.IndexOf("Child Surname")],
            ChildPostCode = eventProps[columnHeaders.IndexOf("Child Postcode")],
            ChildDateOfBirth = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Child DOB")])),
            PartnerNationalInsuranceNumber = eventProps[columnHeaders.IndexOf("Partner NINO")],
            PartnerFirstName = eventProps[columnHeaders.IndexOf("Partner Forename")],
            PartnerLastName = eventProps[columnHeaders.IndexOf("Partner Surname")],
            PartnerDateOfBirth = partnerDateOfBirth,
            SubmissionDate = submissionDate,
            DiscretionaryValidityStartDate = GetDiscretionaryStartDate(validityStartDate, submissionDate),
            GracePeriodEndDate = GetGracePeriodEndDate(validityEndDate)
        };

        return wfEvent;
    }
    //If VED >= 1 Jan  and VED <= 10 Feb then GPED = 31-Mar
    //If VED >= 11 Feb and VED <= 26 May then GPED = 31-Aug 
    //If VED >= 27 May and VED <= 31 August then GPED  = 31-Dec 
    //If VED >= 1 September and VED <= 21 October then GPED = 31-Dec
    //If VED >= 22 October and VED <= 31 Dec then GPED  31-Mar following year
    public static DateTime GetGracePeriodEndDate(DateTime validityEndDate)
    {
        var validityEndDateOnly = validityEndDate.Date;

        if (validityEndDateOnly >= new DateTime(validityEndDateOnly.Year, 10, 22))
        {
            return new DateTime(validityEndDateOnly.Year + 1, 3, 31);
        }
        else if (validityEndDateOnly >= new DateTime(validityEndDateOnly.Year, 5, 27))
        {
            return new DateTime(validityEndDateOnly.Year, 12, 31);
        }
        else if (validityEndDateOnly >= new DateTime(validityEndDateOnly.Year, 2, 11))
        {
            return new DateTime(validityEndDateOnly.Year, 8, 31);
        }
        else
        {
            return new DateTime(validityEndDateOnly.Year, 3, 31);
        }
    }

    // if submitted date is before the current term, and the VSD is < 15 days from the start of the term
    public static DateTime GetDiscretionaryStartDate(DateTime validityStartDate, DateTime submissionDate)
    {
        var firstTermStart = new DateTime(validityStartDate.Year, 9, 1);
        var secondTermStart = new DateTime(validityStartDate.Year, 1, 1);
        var thirdTermStart = new DateTime(validityStartDate.Year, 4, 1);
        var termDates = new List<DateTime> { firstTermStart, secondTermStart, thirdTermStart };

        foreach (DateTime termStart in termDates)
        {
            if (validityStartDate.CompareTo(termStart) >= 0 &&
                validityStartDate.CompareTo(termStart.AddDays(13)) <= 0 &&
                submissionDate.CompareTo(termStart) < 0)
            {
                return termStart.AddDays(-1);
            }
        }
        // Else use VSD
        return validityStartDate;
    }
    /// <summary>
    /// Determines if the contiguity of an event is broken:
    /// If 2 events found (historic and new) the reconfirmation(new event submission date) has happened after the historicEvent VED and the earlier record VSD and VED fall within the same term (code has never been valid).
    /// of if a reconfirmation(new event VSD) has happened after the historicEvent GPED
    /// </summary>
    /// <param name="incomingEvent"></param>
    /// <param name="summaryRecord"></param>
    /// <param name="eventRecordCount"></param>
    /// <returns></returns>
    public static WorkingFamiliesEventSummary EvaluateContiguityForCodeFromIncomingEvent(WorkingFamiliesEvent incomingEvent, WorkingFamiliesEventSummary? summaryRecord, int eventRecordCount)
    {

        //if older events found (summary record is not null), initiate contiguous logic
        if (summaryRecord != null)
        {
            var historicalEventVSDTerm = WorkingFamiliesCheckHelper.GetTerms(summaryRecord.DiscretionaryValidityStartDate);
            var historicalEventVEDTerm = WorkingFamiliesCheckHelper.GetTerms(summaryRecord.ValidityEndDate);

            // if contiguous chain is broken
            if ((eventRecordCount == 2 && incomingEvent.SubmissionDate > summaryRecord.ValidityEndDate
                && historicalEventVSDTerm.Current.Name == historicalEventVEDTerm.Current.Name) ||
                (incomingEvent.DiscretionaryValidityStartDate > summaryRecord.GracePeriodEndDate))
            {
                return MapWorkingFamiliesEventUpdateDatesToSummaryRecord(incomingEvent, summaryRecord, isContiguous: false);
            }
            // continue the chain
            else
            {
                return MapWorkingFamiliesEventUpdateDatesToSummaryRecord(incomingEvent, summaryRecord, isContiguous: true);
            }
        }
        // if no summary event record found, map a new summary record from the incoming event.
        else
        {
            return MapWorkingFamiliesEventToNewSummaryRecord(incomingEvent);
        }
    }

    public static DateTime CalculateValidityStartDate(DateTime submissionDate, WorkingFamiliesEventSummary? existingSummaryRecord)
    {
        if (existingSummaryRecord == null || submissionDate > existingSummaryRecord.ValidityEndDate)
        {
            // If there is no existing summary record or submitted date > summaryRecord.VED, the validity start date should be the submission date
            return submissionDate;
        }
        else
        {
            // If there is an existing summary record, the validity start date should be the day after the existing validity end date
            return existingSummaryRecord.ValidityEndDate.AddDays(1);
        }
    }
}