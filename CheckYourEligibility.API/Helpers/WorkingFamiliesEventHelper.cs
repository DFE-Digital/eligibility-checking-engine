using CheckYourEligibility.API.Domain;
using System.Threading.Tasks;


public static class WorkingFamiliesEventHelper
{
    public static WorkingFamiliesEvent ParseWorkingFamilyFromFosterFamily(FosterFamilyRequest data, string eligibilityCode)
    {

        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent
        {

            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            EligibilityCode = eligibilityCode,
            ValidityStartDate = data.SubmissionDate,
            ValidityEndDate = data.SubmissionDate.AddMonths(3),

            ParentNationalInsuranceNumber = data.FosterCarer.CarerNationalInsuranceNumber,
            ParentFirstName = data.FosterCarer.CarerFirstName,
            ParentLastName = data.FosterCarer.CarerLastName,
            ParentDateOfBirth = data.FosterCarer.CarerDateOfBirth,
            PartnerNationalInsuranceNumber = data.Partner?.PartnerNationalInsuranceNumber ?? string.Empty,
            PartnerFirstName = data.Partner?.PartnerFirstName ?? string.Empty,
            PartnerLastName = data.Partner?.PartnerLastName ?? string.Empty,
            PartnerDateOfBirth = data.Partner?.PartnerDateOfBirth,

            ChildFirstName = data.FosterChild.ChildFirstName,
            ChildLastName = data.FosterChild.ChildLastName,
            ChildPostCode = data.FosterChild.ChildPostCode,
            ChildDateOfBirth = data.FosterChild.ChildDateOfBirth,
            SubmissionDate = data.SubmissionDate,

            DiscretionaryValidityStartDate = GetDiscretionaryStartDate(data.SubmissionDate, data.SubmissionDate), // validity start date and submmission
            GracePeriodEndDate = GetGracePeriodEndDate(data.SubmissionDate.AddMonths(3))
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
    public static WorkingFamiliesEventSummary MapWorkingFamiliesEventUpdateDatesToSummaryRecord(WorkingFamiliesEvent workingFamiliesEvent, WorkingFamiliesEventSummary eventSummary, bool isContiguous = false) {
        DateTime today = DateTime.UtcNow.Date;

     //   eventSummary.FirstCheckLocalAuthorityId = null; // what ? 
     //   eventSummary.HasCodeBeenCheckedByOwningLA = false; // what?
        eventSummary.LastUpdatedDate = today;
        eventSummary.LatestSubmissionDate = workingFamiliesEvent.SubmissionDate;
       // eventSummary.OwningLocalAuthorityId = null; // who, how we are getting this information?
        eventSummary.ValidityEndDate = workingFamiliesEvent.ValidityEndDate;
        eventSummary.GracePeriodEndDate = workingFamiliesEvent.GracePeriodEndDate;



        if (!isContiguous)
        {
            eventSummary.FirstEventDate = today; // right ? 
           // eventSummary.LastCheckDate = null; // if the chain breaks do we set this to null?
           // eventSummary.FirstCheckDate = null; // if the chain breaks do we set this to null ? 
            eventSummary.DiscretionaryValidityStartDate = workingFamiliesEvent.DiscretionaryValidityStartDate;
            eventSummary.ValidityStartDate = workingFamiliesEvent.ValidityStartDate;

        }
        return eventSummary;
    }
    public static WorkingFamiliesEventSummary MapWorkingFamiliesEventToNewSummaryRecord(WorkingFamiliesEvent workingFamiliesEvent) {

        DateTime today = DateTime.UtcNow.Date;
        WorkingFamiliesEventSummary newEventSummary = new WorkingFamiliesEventSummary()
        {
            WorkingFamiliesEventSummaryID = Guid.NewGuid().ToString(),
            EligibilityCode = workingFamiliesEvent.EligibilityCode,
            ChildDateOfBirth = workingFamiliesEvent.ChildDateOfBirth,
            ChildFirstName = workingFamiliesEvent.ChildFirstName,
            ParentNationalInsuranceNumber = workingFamiliesEvent.ParentNationalInsuranceNumber ?? string.Empty, // why do we allow null for the event but not for the summary ? ,
            PartnerNationalInsuranceNumber = workingFamiliesEvent.PartnerNationalInsuranceNumber,
            ChildPostCode = workingFamiliesEvent.ChildPostCode ?? string.Empty, // why do we allow null for the event but not for the summary ?          
            ChildFirstNameTruncated = workingFamiliesEvent.ChildFirstName, // what is the point of this?
            FirstEventDate = today,
            HasCodeBeenCheckedByOwningLA = false,
            LastUpdatedDate = today,
            LatestSubmissionDate = workingFamiliesEvent.SubmissionDate,
            OwningLocalAuthorityId = null, //how do we get this information ?
            GracePeriodEndDate = workingFamiliesEvent.GracePeriodEndDate,
            DiscretionaryValidityStartDate = workingFamiliesEvent.DiscretionaryValidityStartDate,
            ValidityStartDate = workingFamiliesEvent.ValidityStartDate,
            ValidityEndDate = workingFamiliesEvent.ValidityEndDate
        };
        return newEventSummary;

    }

    /// <summary>
    /// Map personal data to the summary record from its event record
    /// </summary>
    /// <param name="workingFamiliesEvent"></param>
    /// <returns></returns>
    public static WorkingFamiliesEventSummary MapPIWorkingFamilySummaryFromWorkingFamilyEvent(WorkingFamiliesEvent workingFamiliesEvent) { 
        
        WorkingFamiliesEventSummary eventSummary = new WorkingFamiliesEventSummary() {  
           ChildDateOfBirth = workingFamiliesEvent.ChildDateOfBirth,  
           ParentNationalInsuranceNumber = workingFamiliesEvent.ParentNationalInsuranceNumber, //why do we allow null for the event but not for the summary ? ,
           PartnerNationalInsuranceNumber = workingFamiliesEvent.PartnerNationalInsuranceNumber,
           ChildPostCode = workingFamiliesEvent.ChildPostCode ?? string.Empty, //why do we allow null for the event but not for the summary ?          
           ChildFirstNameTruncated = workingFamiliesEvent.ChildFirstName,
        };
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
    //If VED => 1 Jan  and VED <= 10 Feb then GPED = 31-Mar
    //If VED => 11 Feb and VED <= 26 May then GPED = 31-Aug 
    //If VED => 27 May and VED <= 31 August then GPED  = 31-Dec 
    //If VED => 1 September and VED <= 21 October then GPED = 31-Dec
    //If VED => 22 October and VED <= 31 Dec then GPED  31-Mar following year
    public static DateTime GetGracePeriodEndDate(DateTime validityEndDate)
    {
        if (validityEndDate.CompareTo(new DateTime(validityEndDate.Year, 10, 22)) >= 0)
        {
            return new DateTime(validityEndDate.Year + 1, 3, 31);
        }
        else if (validityEndDate.CompareTo(new DateTime(validityEndDate.Year, 5, 27)) >= 0)
        {
            return new DateTime(validityEndDate.Year, 12, 31);
        }
        else if (validityEndDate.CompareTo(new DateTime(validityEndDate.Year, 2, 11)) >= 0)
        {
            return new DateTime(validityEndDate.Year, 8, 31);
        }
        else
        {
            return new DateTime(validityEndDate.Year, 3, 31);
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
    /// If only one historic event is found and the reconfirmation(new event VSD) has happened after the hisoricEvent VED
    /// or if more than one historic event is found and the reconfirmation(new event VSD) has happened after the historicEvent GPED
    /// </summary>
    /// <param name="incomingEvent"></param>
    /// <param name="summaryRecord"></param>
    /// <param name="historicEventRecordCount"></param>
    /// <returns></returns>
    public static WorkingFamiliesEventSummary EvaluateContiguityForCodeFromIncomingEvent(WorkingFamiliesEvent incomingEvent, WorkingFamiliesEventSummary? summaryRecord, int historicEventRecordCount) {

       

        //if older events found (summary record is not null), initiate contiguous logic
        // Q LILI: is that safe ? what if historic data does not have a summary record attached to it for whatever reason
        if (summaryRecord != null)
        {                    

            // if contiguous chain is broken
            if ((historicEventRecordCount == 1 && incomingEvent.ValidityStartDate > summaryRecord.ValidityEndDate) ||
                (incomingEvent.ValidityStartDate > summaryRecord.GracePeriodEndDate))
            {
               return MapWorkingFamiliesEventUpdateDatesToSummaryRecord(incomingEvent, summaryRecord, isContiguous: false);
            }
            // continue the chain
            else
            {
                return MapWorkingFamiliesEventUpdateDatesToSummaryRecord(incomingEvent, summaryRecord, isContiguous: true);
            }
        }
        // if no summary event record found, create a new summary record from the incoming event.
        else
        {
           WorkingFamiliesEventSummary eventSummaryRecord = new();
          return  eventSummaryRecord = MapWorkingFamiliesEventToNewSummaryRecord(incomingEvent);
        }
    }
}