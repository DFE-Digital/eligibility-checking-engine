using CheckYourEligibility.API.Domain;


public static class WorkingFamiliesEventHelper
{
    public static WorkingFamiliesEvent ParseWorkingFamilyFromFosterFamily(FosterFamilyRequest data)
    {

        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent
        {

            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            EligibilityCode = $"94{new Random().Next(100000000, 999999999)}", /// temp code
            ValidityStartDate = data.SubmissionDate,
            ValidityEndDate = data.SubmissionDate.AddMonths(3),

            ParentNationalInsuranceNumber = data.FosterCarer.CarerNationalInsuranceNumber,
            ParentFirstName = data.FosterCarer.CarerFirstName,
            ParentLastName = data.FosterCarer.CarerLastName,
            ParentDateOfBirth = data.FosterCarer.CarerDateOfBirth,
            PartnerNationalInsuranceNumber = data.Partner.PartnerNationalInsuranceNumber ?? string.Empty,
            PartnerFirstName = data.Partner.PartnerFirstName ?? string.Empty,
            PartnerLastName = data.Partner.PartnerLastName ?? string.Empty,
            PartnerDateOfBirth = data.Partner.PartnerDateOfBirth,

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

    public static DateTime GetDiscretionaryStartDate(DateTime validityStartDate, DateTime submissionDate)
    {
        var firstTermStart = new DateTime(validityStartDate.Year, 9, 1);
        var secondTermStart = new DateTime(validityStartDate.Year, 1, 1);
        var thirdTermStart = new DateTime(validityStartDate.Year, 4, 1);
        var termDates = new List<DateTime>([firstTermStart, secondTermStart, thirdTermStart]);

        foreach (DateTime termStart in termDates)
        {
            if (validityStartDate.CompareTo(termStart) > 0 &&
                validityStartDate.CompareTo(termStart.AddDays(13)) <= 0 &&
                submissionDate.CompareTo(termStart) < 0)
            {
                return termStart.AddDays(-1);
            }
        }
        // Else use VSD
        return validityStartDate;
    }



}