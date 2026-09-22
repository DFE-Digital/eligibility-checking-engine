using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Factories;
using CheckYourEligibility.API.Gateways.Factories.Helper;
using CheckYourEligibility.API.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.API.Tests.Gateways.Factories;

public class WorkingFamiliesTestScenarioFactoryTests
{
    private static readonly DateTime CheckDate = new(2026, 9, 7);
    private static readonly Term CurrentTerm =
        new(Domain.Enums.WorkingFamilies.TermName.Autumn, new DateTime(2026, 9, 1));

    private readonly TestDataConfiguration _configuration = new();
    private WorkingFamiliesTestScenarioFactory _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _configuration.CannotBeUsedYet = "700";
        _configuration.ValidForThisTerm = "701";
        _configuration.ValidForThisTermAndNextTerm = "702";
        _configuration.InGracePeriod = "703";
        _configuration.IsExpired = "704";
        _configuration.ApplyDvsdNINOPrefix = "NN";
        _configuration.ReconfirmationStatusDueNowNINOSuffix = "C";

        _sut = new WorkingFamiliesTestScenarioFactory(_configuration, NullLoggerFactory.Instance);
    }

    [TestCase("70000000000", "AB123456A")]
    [TestCase("70100000000", "AB123456A")]
    [TestCase("70200000000", "AB123456A")]
    [TestCase("70300000000", "AB123456A")]
    [TestCase("70400000000", "AB123456A")]
    public void GenerateTestScenarioInternalSide_GeneratesConfiguredScenario(string eligibilityCode, string nino)
    {
        var checkDate = CheckDate;
        var currentTerm = CurrentTerm;
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData(eligibilityCode, nino), CheckDate);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.EligibilityCode, Is.EqualTo(eligibilityCode));
        Assert.That(result.SubmissionDate, Is.EqualTo(result.ValidityStartDate));
        Assert.That(result.DiscretionaryValidityStartDate, Is.EqualTo(result.ValidityStartDate));

        switch (eligibilityCode[..3])
        {
            case "700": //cannot be used yet
                Assert.That(result.ValidityStartDate, Is.GreaterThan(currentTerm.StartDate));
                Assert.That(result.ValidityEndDate, Is.EqualTo(checkDate.AddMonths(3)));
                Assert.That(result.GracePeriodEndDate,
                    Is.EqualTo(WorkingFamiliesEventHelper.GetGracePeriodEndDate(result.ValidityEndDate)));
                break;
            case "701": // valid for this term only
                Assert.That(result.ValidityStartDate,
                    Is.InRange(currentTerm.StartDate.AddDays(-28), currentTerm.StartDate.AddDays(-1)));
                Assert.That(result.ValidityEndDate, Is.InRange(checkDate.AddDays(1), GetCurrentTermEndDate(currentTerm)));
                Assert.That(result.GracePeriodEndDate, Is.EqualTo(GetCurrentTermEndDate(currentTerm)));
                break;
            case "702": // valid for this term and next
                Assert.That(result.ValidityStartDate,
                    Is.InRange(currentTerm.StartDate.AddDays(-28), currentTerm.StartDate.AddDays(-1)));
                Assert.That(result.GracePeriodEndDate, Is.GreaterThan(new DateTime(2027, 1, 1)));
                break;
            case "703": // in grace period
                Assert.That(result.ValidityStartDate, Is.EqualTo(currentTerm.StartDate.AddDays(-1)));
                Assert.That(result.ValidityEndDate, Is.LessThan(checkDate));
                Assert.That(result.GracePeriodEndDate, Is.GreaterThan(checkDate));
                break;
            case "704": // expired
                Assert.That(result.ValidityStartDate, Is.LessThan(result.ValidityEndDate));
                Assert.That(result.ValidityEndDate, Is.LessThan(checkDate));
                Assert.That(result.GracePeriodEndDate, Is.LessThan(checkDate));
                break;
        }
    }

    [Test]
    public void GenerateTestScenarioInternalSide_CannotBeUsedYet_StartsAfterCurrentTerm()
    {
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70000000000"), CheckDate);
      
        Assert.That(result!.ValidityStartDate, Is.EqualTo(CurrentTerm.StartDate.AddDays(15)));
        Assert.That(result.ValidityEndDate, Is.EqualTo(CheckDate.AddMonths(3)));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ValidThisTermOnly_DueNowNino_GeneratesEndDateInDueWindow()
    {
        var termEndDate = GetCurrentTermEndDate(CurrentTerm);
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70100000000", "AB123456C"), CheckDate);

        Assert.That(result!.ValidityEndDate, Is.GreaterThanOrEqualTo(CheckDate));
        Assert.That(result.ValidityEndDate, Is.LessThanOrEqualTo(CheckDate.AddDays(28)));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(termEndDate));
    }


    [Test]
    public void GenerateTestScenarioInternalSide_ValidThisTermOnly_WhenMinVEDIsBeforeTermEndDate_ReturnsVEDAfterStartOfReconfirmWindow()
    {
        var termEndDate = GetCurrentTermEndDate(CurrentTerm);
        var minVed = CheckDate.AddDays(31);

        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70100000000", "AB123456A"), CheckDate);

        Assert.That(result!.ValidityEndDate, Is.InRange(minVed, termEndDate));
        Assert.That(result.ValidityEndDate, Is.GreaterThan(CheckDate.AddDays(28)));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(termEndDate));
    }

    [TestCase(2026, 12, 20)]
    [TestCase(2026, 11, 30)]
    public void GenerateTestScenarioInternalSide_ValidThisTermOnly_WhenMinVEDGreaterThenOrEqualToTermEndDate_ReturnsVEDEqualToTermEndDate(int year, int month, int day)
    {
        var termEndDate = GetCurrentTermEndDate(CurrentTerm);
        DateTime lateTermCheckDate = new(year, month, day);
        var result = _sut.GenerateTestScenarioInternalSide(
              CreateCheckData("70100000000", "AB123456A"), lateTermCheckDate);

        Assert.That(result.ValidityEndDate, Is.EqualTo(termEndDate));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(termEndDate));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ApplyDvsdNino_SpringTerm_UsesPreviousDecemberDvsd()
    {
        var result = _sut.GenerateTestScenarioInternalSide(
            CreateCheckData("70100000000", "NN123456A"), new DateTime(2026, 2, 15));

        Assert.That(result!.DiscretionaryValidityStartDate, Is.EqualTo(new DateTime(2025, 12, 31))); //Winter
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ApplyDvsdNino_SummerTerm_UsesPreviousMarchDvsd()
    {
        var result = _sut.GenerateTestScenarioInternalSide(
            CreateCheckData("70100000000", "NN123456A"), new DateTime(2026, 6, 15));

        Assert.That(result!.DiscretionaryValidityStartDate, Is.EqualTo(new DateTime(2026, 3, 31))); //Spring
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ApplyDvsdNino_AutumnTerm_UsesPreviousAugustDvsd()
    {
        var result = _sut.GenerateTestScenarioInternalSide(
            CreateCheckData("70100000000", "NN123456A"), new DateTime(2026, 10, 15));

        Assert.That(result!.DiscretionaryValidityStartDate, Is.EqualTo(new DateTime(2026, 8, 31))); //Summer
    }
    [Test]
    public void GenerateTestScenarioInternalSide_DoNotApplyDvsdNino_ForCannotBeUsetYetScenario()
    {
        var result = _sut.GenerateTestScenarioInternalSide(
            CreateCheckData("70010000000", "NN123456A"), new DateTime(2026, 10, 15));

        Assert.That(result!.DiscretionaryValidityStartDate, Is.EqualTo(result.ValidityStartDate));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ExpiredReconfirmationOverdue_JanuaryCheckDate_GeneratesExpectedDates()
    {
        AssertExpiredReconfirmationScenario(
            new DateTime(2026, 1, 15), // check date
            new DateTime(2025, 7, 31), // expected VSD
            new DateTime(2025, 9, 1), // minimum VED
            new DateTime(2025, 10, 21), // maximum VED
            new DateTime(2025, 12, 31)); // expected GPED
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ExpiredReconfirmationOverdue_FebruaryCheckDate_GeneratesExpectedDates()
    {
        AssertExpiredReconfirmationScenario(
            new DateTime(2026, 2, 15), // check date
            new DateTime(2025, 8, 30), // expected VSD
            new DateTime(2025, 9, 1), // minimum VED
            new DateTime(2025, 10, 21), // maximum VED
            new DateTime(2025, 12, 31)); // expected GPED
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ExpiredReconfirmationOverdue_JuneCheckDate_GeneratesExpectedDates()
    {
        AssertExpiredReconfirmationScenario(
            new DateTime(2026, 6, 15), // check date
            new DateTime(2025, 1, 1), // expected VSD
            new DateTime(2025, 10, 22), // minimum VED
            new DateTime(2025, 12, 31), // maximum VED
            new DateTime(2026, 3, 31)); // expected GPED
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ExpiredReconfirmationOverdue_SeptemberCheckDate_GeneratesExpectedDates()
    {
        AssertExpiredReconfirmationScenario(
            new DateTime(2026, 9, 15), // check date
            new DateTime(2026, 1, 20), // expected VSD
            new DateTime(2026, 2, 11), // minimum VED
            new DateTime(2026, 5, 26), // maximum VED
            new DateTime(2026, 8, 31)); // expected GPED
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ExpiredReconfirmationOverdue_NovemberCheckDate_GeneratesExpectedDates()
    {
        AssertExpiredReconfirmationScenario(
            new DateTime(2026, 11, 15), // check date
            new DateTime(2026, 3, 1), // expected VSD
            new DateTime(2026, 5, 1), // minimum VED
            new DateTime(2026, 5, 26), // maximum VED
            new DateTime(2026, 8, 31)); // expected GPED
    }

    [Test]
    public void GenerateTestScenarioInternalSide_WhenCodeDoesNotMatchScenario_ReturnsNull()
    {
        Assert.That(
            () => _sut.GenerateTestScenarioInternalSide(CreateCheckData("99900000000"), CheckDate), Is.Null);
    }
    #region Private
    private void AssertExpiredReconfirmationScenario(
        DateTime checkDate,
        DateTime expectedValidityStartDate,
        DateTime minimumValidityEndDate,
        DateTime maximumValidityEndDate,
        DateTime expectedGracePeriodEndDate)
    {
        var result = _sut.GenerateTestScenarioInternalSide(
            CreateCheckData("70400000000"), checkDate);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ValidityStartDate, Is.EqualTo(expectedValidityStartDate));
        Assert.That(result.ValidityEndDate, Is.InRange(minimumValidityEndDate, maximumValidityEndDate));
        Assert.That(result.ValidityEndDate, Is.GreaterThan(result.ValidityStartDate));
        Assert.That(result.GracePeriodEndDate, Is.EqualTo(expectedGracePeriodEndDate));
        Assert.That(result.GracePeriodEndDate, Is.LessThan(checkDate));
    }

    private static CheckProcessData CreateCheckData(string eligibilityCode, string nino = "AB123456A") => new()
    {
        EligibilityCode = eligibilityCode,
        NationalInsuranceNumber = nino
    };

    private static DateTime GetCurrentTermEndDate(Term currentTerm) => currentTerm.Name switch
    {
        Domain.Enums.WorkingFamilies.TermName.Spring => new DateTime(currentTerm.StartDate.Year, 3, 31),
        Domain.Enums.WorkingFamilies.TermName.Summer => new DateTime(currentTerm.StartDate.Year, 8, 31),
        Domain.Enums.WorkingFamilies.TermName.Autumn => new DateTime(currentTerm.StartDate.Year, 12, 31),
        _ => throw new InvalidOperationException()
    };
    #endregion
}