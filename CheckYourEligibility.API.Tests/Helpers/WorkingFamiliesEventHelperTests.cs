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
}