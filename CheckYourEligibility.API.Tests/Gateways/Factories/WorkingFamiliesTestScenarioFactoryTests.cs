using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Factories;
using CheckYourEligibility.API.Gateways.Factories.Helper;
using CheckYourEligibility.API.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.API.Tests.Gateways.Factories;

public class WorkingFamiliesTestScenarioFactoryTests
{
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
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData(eligibilityCode, nino));
        var checkDate = DateTime.Today;
        var currentTerm = WorkingFamiliesCheckHelper.GetTerms(checkDate).Current;

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
                Assert.That(result.GracePeriodEndDate, Is.GreaterThan(WorkingFamiliesCheckHelper.GetTerms(checkDate).Next.StartDate));
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
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70000000000"));
        var currentTerm = WorkingFamiliesCheckHelper.GetTerms(DateTime.Today).Current;

        Assert.That(result!.ValidityStartDate, Is.EqualTo(currentTerm.StartDate.AddDays(15)));
        Assert.That(result.ValidityEndDate, Is.EqualTo(DateTime.Today.AddMonths(3)));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_DueNowNino_GeneratesEndDateInDueWindow()
    {
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70100000000", "AB123456C"));

        Assert.That(result!.ValidityEndDate, Is.GreaterThanOrEqualTo(DateTime.Today));
        Assert.That(result.ValidityEndDate, Is.LessThanOrEqualTo(DateTime.Today.AddDays(28)));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_NinoNotDueNow_GeneratesEndDateOutsideDueWindow()
    {
        var checkDate = DateTime.Today;
        var currentTerm = WorkingFamiliesCheckHelper.GetTerms(checkDate).Current;
        var termEndDate = GetCurrentTermEndDate(currentTerm);
        var minVed = checkDate.AddDays(29);

        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70100000000", "AB123456A"));

        if (minVed <= termEndDate)
        {
            Assert.That(result!.ValidityEndDate, Is.InRange(minVed, termEndDate));
        }
        else
        {
            Assert.That(result!.ValidityEndDate,
                Is.InRange(currentTerm.StartDate, checkDate.AddDays(-29)));
        }

        Assert.That(checkDate,
            Is.LessThan(result.ValidityEndDate.AddDays(-28))
                .Or.GreaterThan(result.ValidityEndDate));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_ApplyDvsdNino_UsesTermDvsd()
    {
        var result = _sut.GenerateTestScenarioInternalSide(CreateCheckData("70100000000", "NN123456A"));
        var currentTerm = WorkingFamiliesCheckHelper.GetTerms(DateTime.Today).Current;

        var expectedDvsd = currentTerm.Name switch
        {
            Domain.Enums.WorkingFamilies.TermName.Spring => new DateTime(currentTerm.StartDate.Year - 1, 12, 31),
            Domain.Enums.WorkingFamilies.TermName.Summer => new DateTime(currentTerm.StartDate.Year, 3, 31),
            Domain.Enums.WorkingFamilies.TermName.Autumn => new DateTime(currentTerm.StartDate.Year, 8, 31),
            _ => throw new InvalidOperationException()
        };

        Assert.That(result!.DiscretionaryValidityStartDate, Is.EqualTo(expectedDvsd));
    }

    [Test]
    public void GenerateTestScenarioInternalSide_WhenCodeDoesNotMatchScenario_Throws()
    {
        Assert.That(
            () => _sut.GenerateTestScenarioInternalSide(CreateCheckData("99900000000")), Is.Null);
    }
    #region Private
    private static CheckProcessData CreateCheckData(string eligibilityCode, string nino = "AB123456A") => new()
    {
        EligibilityCode = eligibilityCode,
        NationalInsuranceNumber = nino
    };

    private static DateTime GetCurrentTermEndDate(WorkingFamiliesCheckHelper.Term currentTerm) => currentTerm.Name switch
    {
        Domain.Enums.WorkingFamilies.TermName.Spring => new DateTime(currentTerm.StartDate.Year, 3, 31),
        Domain.Enums.WorkingFamilies.TermName.Summer => new DateTime(currentTerm.StartDate.Year, 8, 31),
        Domain.Enums.WorkingFamilies.TermName.Autumn => new DateTime(currentTerm.StartDate.Year, 12, 31),
        _ => throw new InvalidOperationException()
    };
    #endregion
}