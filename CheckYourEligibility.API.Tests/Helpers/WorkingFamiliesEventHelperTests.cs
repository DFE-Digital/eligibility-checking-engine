using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Tests.Helpers;

public class WorkingFamiliesEventHelperTests
{
    [Test]
    public void ParseWorkingFamilyFromFosterFamily_MapsFamilyDetailsAndCalculatedDates()
    {
        var submissionDate = new DateTime(2026, 8, 20);
        var request = new FosterFamilyRequest
        {
            SubmissionDate = submissionDate,
            FosterCarer = new FosterCarerRequest
            {
                CarerFirstName = "Alex",
                CarerLastName = "Foster",
                CarerDateOfBirth = new DateTime(1980, 2, 3),
                CarerNationalInsuranceNumber = "ab 12 34 56 c"
            },
            Partner = new FosterPartnerRequest
            {
                PartnerFirstName = "Pat",
                PartnerLastName = "Foster",
                PartnerDateOfBirth = new DateTime(1982, 4, 5),
                PartnerNationalInsuranceNumber = "cd 65 43 21 e"
            },
            FosterChild = new FosterChildRequest
            {
                ChildFirstName = "Casey",
                ChildLastName = "Foster",
                ChildDateOfBirth = new DateTime(2022, 6, 7),
                ChildPostCode = "AB1 2CD"
            }
        };

        var result = WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(request, "12345678901");

        Assert.That(result.WorkingFamiliesEventID, Is.Not.Empty);
        Assert.That(result.EligibilityCode, Is.EqualTo("12345678901"));
        Assert.That(result.ParentNationalInsuranceNumber, Is.EqualTo("AB123456C"));
        Assert.That(result.ParentFirstName, Is.EqualTo("Alex"));
        Assert.That(result.ParentLastName, Is.EqualTo("Foster"));
        Assert.That(result.ParentDateOfBirth, Is.EqualTo(new DateTime(1980, 2, 3)));
        Assert.That(result.PartnerNationalInsuranceNumber, Is.EqualTo("CD654321E"));
        Assert.That(result.PartnerFirstName, Is.EqualTo("Pat"));
        Assert.That(result.PartnerLastName, Is.EqualTo("Foster"));
        Assert.That(result.PartnerDateOfBirth, Is.EqualTo(new DateTime(1982, 4, 5)));
        Assert.That(result.ChildFirstName, Is.EqualTo("Casey"));
        Assert.That(result.ChildLastName, Is.EqualTo("Foster"));
        Assert.That(result.ChildPostCode, Is.EqualTo("AB1 2CD"));
        Assert.That(result.ChildDateOfBirth, Is.EqualTo(new DateTime(2022, 6, 7)));
        Assert.That(result.SubmissionDate, Is.EqualTo(submissionDate));
        Assert.That(result.ValidityStartDate, Is.EqualTo(submissionDate));
        Assert.That(result.ValidityEndDate, Is.EqualTo(new DateTime(2026, 11, 20)));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(submissionDate));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(new DateTime(2027, 3, 31)));
    }

    [Test]
    public void ParseWorkingFamilyFromFosterFamily_WithoutPartner_UsesEmptyPartnerStrings()
    {
        var request = new FosterFamilyRequest
        {
            SubmissionDate = new DateTime(2026, 1, 10),
            FosterCarer = new FosterCarerRequest(),
            FosterChild = new FosterChildRequest()
        };

        var result = WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(request, "code");

        Assert.That(result.PartnerNationalInsuranceNumber, Is.EqualTo(string.Empty));
        Assert.That(result.PartnerFirstName, Is.EqualTo(string.Empty));
        Assert.That(result.PartnerLastName, Is.EqualTo(string.Empty));
        Assert.That(result.PartnerDateOfBirth, Is.Null);
    }

    [Test]
    public void ParseWorkingFamiliesEvent_ParsesExcelDatesAndOptionalPartnerDob()
    {
        var headers = new List<string>
        {
            "Eligibility Code", "Validity Start Date", "Validity End Date", "Submission Date",
            "Parent NINO", "Parent Forename", "Parent Surname", "Parent DOB",
            "Child Forename", "Child Surname", "Child Postcode", "Child DOB",
            "Partner NINO", "Partner Forename", "Partner Surname", "Partner DOB"
        };
        var validityStartDate = new DateTime(2026, 9, 5);
        var validityEndDate = new DateTime(2026, 12, 5);
        var submissionDate = new DateTime(2026, 8, 30);
        var values = new List<string>
        {
            "70100000000", validityStartDate.ToOADate().ToString(), validityEndDate.ToOADate().ToString(),
            submissionDate.ToOADate().ToString(), "AB123456C", "Alex", "Foster",
            new DateTime(1980, 2, 3).ToOADate().ToString(), "Casey", "Foster", "AB1 2CD",
            new DateTime(2022, 6, 7).ToOADate().ToString(), "", "", "", ""
        };

        var result = WorkingFamiliesEventHelper.ParseWorkingFamiliesEvent(values, headers);

        Assert.That(result.EligibilityCode, Is.EqualTo("70100000000"));
        Assert.That(result.ValidityStartDate, Is.EqualTo(validityStartDate));
        Assert.That(result.ValidityEndDate, Is.EqualTo(validityEndDate));
        Assert.That(result.SubmissionDate, Is.EqualTo(submissionDate));
        Assert.That(result.ParentDateOfBirth, Is.EqualTo(new DateTime(1980, 2, 3)));
        Assert.That(result.ChildDateOfBirth, Is.EqualTo(new DateTime(2022, 6, 7)));
        Assert.That(result.PartnerNationalInsuranceNumber, Is.Empty);
        Assert.That(result.PartnerDateOfBirth, Is.Null);
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(new DateTime(2026, 8, 31)));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(new DateTime(2027, 3, 31)));
    }


    //If VED >= 1 Jan  and VED <= 10 Feb then GPED = 31-Mar
    //If VED >= 11 Feb and VED <= 26 May then GPED = 31-Aug 
    //If VED >= 27 May and VED <= 31 August then GPED  = 31-Dec 
    //If VED >= 1 September and VED <= 21 October then GPED = 31-Dec
    //If VED >= 22 October and VED <= 31 Dec then GPED  31-Mar following year

    [TestCase(2026, 10, 21, 2026, 12, 31)]
    [TestCase(2026, 10, 22, 2027, 3, 31)]
    [TestCase(2026, 5, 26, 2026, 8, 31)]
    [TestCase(2026, 5, 27, 2026, 12, 31)]
    [TestCase(2026, 2, 10, 2026, 3, 31)]
    [TestCase(2026, 2, 11, 2026, 8, 31)]
    public void GetGracePeriodEndDate_ReturnsExpectedTermEnd(
        int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        var result = WorkingFamiliesEventHelper.GetGracePeriodEndDate(new DateTime(year, month, day));

        Assert.That(result, Is.EqualTo(new DateTime(expectedYear, expectedMonth, expectedDay)));
    }

    [TestCase(2026, 1, 1,2025, 12, 31, 2025, 12, 31)]
    [TestCase(2026, 4, 14, 2026, 3, 31, 2026, 3, 31)]
    [TestCase(2026, 9, 14, 2026, 8, 31, 2026, 8, 31)]
    public void GetDiscretionaryStartDate_DuringTermOpeningWindow_UsesPreviousTermEnd(
        int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay,
        int submissionDateYear, int submissionDateMonth, int submissionDateDay)
    {
        var validityStartDate = new DateTime(year, month, day);
        var submissionDate =  new DateTime(submissionDateYear, submissionDateMonth, submissionDateDay);

        var result = WorkingFamiliesEventHelper.GetDiscretionaryStartDate(validityStartDate, submissionDate);

        Assert.That(result, Is.EqualTo(new DateTime(expectedYear, expectedMonth, expectedDay)));
    }

    [Test]
    public void GetDiscretionaryStartDate_OutsideTermOpeningWindow_UsesValidityStartDate()
    {
        var validityStartDate = new DateTime(2026, 9, 15);

        var result = WorkingFamiliesEventHelper.GetDiscretionaryStartDate(
            validityStartDate, validityStartDate.AddDays(-1));

        Assert.That(result, Is.EqualTo(validityStartDate));
    }

    [Test]
    public void MapWorkingFamiliesEventUpdateDatesToSummaryRecord_WhenNotContiguous_ResetsFirstEventDateAndDates()
    {
        var eventSummary = new WorkingFamiliesEventSummary
        {
            WorkingFamiliesEventSummaryID = "summary-1",
            EligibilityCode = "70100000000",
            ValidityEndDate = new DateTime(2026, 6, 30),
            GracePeriodEndDate = new DateTime(2026, 12, 31),
            FirstEventDate = new DateTime(2026, 1, 10),
            ValidityStartDate = new DateTime(2026, 1, 2),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 2)
        };

        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            SubmissionDate = new DateTime(2026, 8, 15),
            ValidityStartDate = new DateTime(2026, 9, 3),
            ValidityEndDate = new DateTime(2026, 12, 3),
            DiscretionaryValidityStartDate = new DateTime(2026, 8, 31),
            GracePeriodEndDate = new DateTime(2027, 3, 31)
        };

        var before = DateTime.UtcNow.Date;
        var result = WorkingFamiliesEventHelper.MapWorkingFamiliesEventUpdateDatesToSummaryRecord(incomingEvent, eventSummary, false);
        var after = DateTime.UtcNow.Date;

        Assert.That(result, Is.SameAs(eventSummary));
        Assert.That(eventSummary.LastUpdatedDate, Is.InRange(before, after));
        Assert.That(eventSummary.LatestSubmissionDate, Is.EqualTo(new DateTime(2026, 8, 15)));
        Assert.That(eventSummary.ValidityEndDate, Is.EqualTo(new DateTime(2026, 12, 3)));
        Assert.That(eventSummary.GracePeriodEndDate, Is.EqualTo(new DateTime(2027, 3, 31)));
        Assert.That(eventSummary.FirstEventDate, Is.InRange(before, after));
        Assert.That(eventSummary.ValidityStartDate, Is.EqualTo(new DateTime(2026, 9, 3)));
        Assert.That(eventSummary.DiscretionaryValidityStartDate, Is.EqualTo(new DateTime(2026, 8, 31)));
    }

    [Test]
    public void MapWorkingFamiliesEventUpdateDatesToSummaryRecord_WhenContiguous_KeepsOriginalSummaryDates()
    {
        var eventSummary = new WorkingFamiliesEventSummary
        {
            WorkingFamiliesEventSummaryID = "summary-2",
            EligibilityCode = "70100000000",
            ValidityStartDate = new DateTime(2026, 1, 2),
            ValidityEndDate = new DateTime(2026, 6, 30),
            GracePeriodEndDate = new DateTime(2026, 12, 31),
            FirstEventDate = new DateTime(2026, 1, 2),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 2)
        };

        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            SubmissionDate = new DateTime(2026, 7, 2),
            ValidityStartDate = new DateTime(2026, 7, 1),
            ValidityEndDate = new DateTime(2026, 12, 31),
            DiscretionaryValidityStartDate = new DateTime(2026, 7, 1),
            GracePeriodEndDate = new DateTime(2027, 3, 31)
        };

        var result = WorkingFamiliesEventHelper.MapWorkingFamiliesEventUpdateDatesToSummaryRecord(incomingEvent, eventSummary, true);

        Assert.That(result, Is.SameAs(eventSummary));
        Assert.That(eventSummary.LastUpdatedDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(eventSummary.LatestSubmissionDate,  Is.EqualTo(incomingEvent.SubmissionDate));
        Assert.That(eventSummary.ValidityEndDate, Is.EqualTo(incomingEvent.ValidityEndDate));
        Assert.That(eventSummary.GracePeriodEndDate, Is.EqualTo(incomingEvent.GracePeriodEndDate));
        Assert.That(eventSummary.FirstEventDate, Is.EqualTo(eventSummary.FirstEventDate));
        Assert.That(eventSummary.ValidityStartDate, Is.EqualTo(eventSummary.ValidityStartDate));
        Assert.That(eventSummary.DiscretionaryValidityStartDate, Is.EqualTo(eventSummary.DiscretionaryValidityStartDate));
    }

    [Test]
    public void MapWorkingFamiliesEventToNewSummaryRecord_CreatesSummaryFromIncomingEvent()
    {
        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            ChildDateOfBirth = new DateTime(2022, 6, 7),
            ChildFirstName = "Casey",
            ParentNationalInsuranceNumber = "AB123456C",
            PartnerNationalInsuranceNumber = "CD654321E",
            ChildPostCode = "AB1 2CD",
            SubmissionDate = new DateTime(2026, 8, 20),
            ValidityStartDate = new DateTime(2026, 8, 20),
            ValidityEndDate = new DateTime(2026, 11, 20),
            DiscretionaryValidityStartDate = new DateTime(2026, 8, 20),
            GracePeriodEndDate = new DateTime(2027, 3, 31)
        };

        var result = WorkingFamiliesEventHelper.MapWorkingFamiliesEventToNewSummaryRecord(incomingEvent);

        Assert.That(result.WorkingFamiliesEventSummaryID, Is.Not.Empty);
        Assert.That(result.EligibilityCode, Is.EqualTo("70100000000"));
        Assert.That(result.ChildDateOfBirth, Is.EqualTo(new DateTime(2022, 6, 7)));
        Assert.That(result.ChildFirstName, Is.EqualTo("Casey"));
        Assert.That(result.ParentNationalInsuranceNumber, Is.EqualTo("AB123456C"));
        Assert.That(result.PartnerNationalInsuranceNumber, Is.EqualTo("CD654321E"));
        Assert.That(result.ChildPostCode, Is.EqualTo("AB1 2CD"));
        Assert.That(result.ChildFirstNameTruncated, Is.EqualTo("Casey"));
        Assert.That(result.FirstCheckDate, Is.Null);
        Assert.That(result.FirstEventDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(result.HasCodeBeenCheckedByOwningLA, Is.False);
        Assert.That(result.LastCheckDate, Is.Null);
        Assert.That(result.LastUpdatedDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(result.LatestSubmissionDate, Is.EqualTo(new DateTime(2026, 8, 20)));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(new DateTime(2027, 3, 31)));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(new DateTime(2026, 8, 20)));
        Assert.That(result.ValidityStartDate, Is.EqualTo(new DateTime(2026, 8, 20)));
        Assert.That(result.ValidityEndDate, Is.EqualTo(new DateTime(2026, 11, 20)));
    }

    [Test]
    public void MapPIWorkingFamilySummaryFromWorkingFamilyEvent_MapsPersonalDataAndNullSafePostcode()
    {
        var incomingEvent = new WorkingFamiliesEvent
        {
            ChildDateOfBirth = new DateTime(2022, 6, 7),
            ChildFirstName = "Casey",
            ParentNationalInsuranceNumber = "AB123456C",
            PartnerNationalInsuranceNumber = "CD654321E",
            ChildPostCode = null,
        };

        var result = WorkingFamiliesEventHelper.MapPIWorkingFamilySummaryFromWorkingFamilyEvent(incomingEvent);

        Assert.That(result.ChildDateOfBirth, Is.EqualTo(new DateTime(2022, 6, 7)));
        Assert.That(result.ParentNationalInsuranceNumber, Is.EqualTo("AB123456C"));
        Assert.That(result.PartnerNationalInsuranceNumber, Is.EqualTo("CD654321E"));
        Assert.That(result.ChildPostCode, Is.EqualTo(string.Empty));
        Assert.That(result.ChildFirstNameTruncated, Is.EqualTo("Casey"));
    }

    [Test]
    public void EvaluateContiguityForCodeFromIncomingEvent_WhenNoSummaryRecord_CreatesNewSummary()
    {
        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            ChildDateOfBirth = new DateTime(2022, 6, 7),
            ChildFirstName = "Casey",
            ParentNationalInsuranceNumber = "AB123456C",
            ChildPostCode = "AB1 2CD",
            SubmissionDate = new DateTime(2026, 8, 20),
            ValidityStartDate = new DateTime(2026, 8, 20),
            ValidityEndDate = new DateTime(2026, 11, 20),
            DiscretionaryValidityStartDate = new DateTime(2026, 8, 20),
            GracePeriodEndDate = new DateTime(2027, 3, 31)
        };

        var result = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(incomingEvent, null, 0);

        Assert.That(result.EligibilityCode, Is.EqualTo(incomingEvent.EligibilityCode));
        Assert.That(result.ChildFirstName, Is.EqualTo(incomingEvent.ChildFirstName));
        Assert.That(result.ChildDateOfBirth, Is.EqualTo(incomingEvent.ChildDateOfBirth));
        Assert.That(result.ParentNationalInsuranceNumber, Is.EqualTo(incomingEvent.ParentNationalInsuranceNumber));
        Assert.That(result.FirstEventDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(result.LatestSubmissionDate, Is.EqualTo(incomingEvent.SubmissionDate));
        Assert.That(result.ValidityStartDate, Is.EqualTo(incomingEvent.ValidityStartDate));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(incomingEvent.DiscretionaryValidityStartDate));
        Assert.That(result.ValidityEndDate, Is.EqualTo(incomingEvent.ValidityEndDate));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(incomingEvent.GracePeriodEndDate));

    }

    [Test]
    public void EvaluateContiguityForCodeFromIncomingEvent_WhenSingleHistoricEventBreaksAfterPreviousValidityEnd_ShouldBreakChain()
    {
        var summary = new WorkingFamiliesEventSummary
        {
            EligibilityCode = "70100000000",
            ValidityEndDate = new DateTime(2026, 6, 30),
            GracePeriodEndDate = new DateTime(2026, 8, 31),
            FirstEventDate = new DateTime(2026, 1, 10),
            ValidityStartDate = new DateTime(2026, 1, 2),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 2),
            LatestSubmissionDate = new DateTime(2026, 6, 15)
        };

        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            SubmissionDate = new DateTime(2026, 9, 10),
            ValidityStartDate = new DateTime(2026, 9, 1),
            ValidityEndDate = new DateTime(2026, 12, 1),
            DiscretionaryValidityStartDate = new DateTime(2026, 9, 1),
            GracePeriodEndDate = new DateTime(2026, 12, 31)
        };

        var result = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(incomingEvent, summary, 1);

        Assert.That(result.FirstEventDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(result.ValidityStartDate, Is.EqualTo(incomingEvent.ValidityStartDate));
        Assert.That(result.ValidityEndDate, Is.EqualTo(incomingEvent.ValidityEndDate));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(incomingEvent.DiscretionaryValidityStartDate));
        Assert.That(result.LatestSubmissionDate, Is.EqualTo(incomingEvent.SubmissionDate));
    }

    [Test]
    public void EvaluateContiguityForCodeFromIncomingEvent_WhenHistoricEventsExistAndNewEventStartsAfterGracePeriod_ShouldBreakChain()
    {
        var summary = new WorkingFamiliesEventSummary
        {
            EligibilityCode = "70100000000",
            ValidityEndDate = new DateTime(2026, 6, 30),
            GracePeriodEndDate = new DateTime(2026, 8, 31),
            FirstEventDate = new DateTime(2026, 1, 10),
            ValidityStartDate = new DateTime(2026, 1, 2),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 2),
            LatestSubmissionDate = new DateTime(2026, 6, 15)
        };

        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            SubmissionDate = new DateTime(2026, 9, 10),
            ValidityStartDate = new DateTime(2026, 9, 1),
            ValidityEndDate = new DateTime(2026, 12, 1),
            DiscretionaryValidityStartDate = new DateTime(2026, 9, 1),
            GracePeriodEndDate = new DateTime(2026, 12, 31)
        };

        var result = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(incomingEvent, summary, 2);

        Assert.That(result.FirstEventDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(result.ValidityStartDate, Is.EqualTo(incomingEvent.ValidityStartDate));
        Assert.That(result.ValidityEndDate, Is.EqualTo(incomingEvent.ValidityEndDate));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(incomingEvent.DiscretionaryValidityStartDate));
        Assert.That(result.LatestSubmissionDate, Is.EqualTo(incomingEvent.SubmissionDate));
    }

    [Test]
    public void EvaluateContiguityForCodeFromIncomingEvent_WhenChainIsContiguous_UsesExistingSummary()
    {
        var summary = new WorkingFamiliesEventSummary
        {
            EligibilityCode = "70100000000",
            ValidityEndDate = new DateTime(2026, 7, 31),
            GracePeriodEndDate = new DateTime(2026, 12, 31),
            FirstEventDate = new DateTime(2026, 1, 10),
            ValidityStartDate = new DateTime(2026, 1, 2),
            DiscretionaryValidityStartDate = new DateTime(2026, 1, 2),
            LatestSubmissionDate = new DateTime(2026, 7, 10)
        };

        var incomingEvent = new WorkingFamiliesEvent
        {
            EligibilityCode = "70100000000",
            SubmissionDate = new DateTime(2026, 8, 10),
            ValidityStartDate = new DateTime(2026, 8, 1),
            ValidityEndDate = new DateTime(2026, 11, 1),
            DiscretionaryValidityStartDate = new DateTime(2026, 8, 1),
            GracePeriodEndDate = new DateTime(2027, 3, 31)
        };

        var result = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(incomingEvent, summary, 2);

        Assert.That(result, Is.SameAs(summary));
        Assert.That(result.LastUpdatedDate, Is.EqualTo(DateTime.UtcNow.Date));
        Assert.That(result.LatestSubmissionDate, Is.EqualTo(incomingEvent.SubmissionDate));
        Assert.That(result.ValidityEndDate, Is.EqualTo(incomingEvent.ValidityEndDate));
        Assert.That(result.ValidityStartDate, Is.EqualTo(summary.ValidityStartDate));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(summary.DiscretionaryValidityStartDate));
        Assert.That(result.FirstEventDate, Is.EqualTo(summary.FirstEventDate));
    }
}