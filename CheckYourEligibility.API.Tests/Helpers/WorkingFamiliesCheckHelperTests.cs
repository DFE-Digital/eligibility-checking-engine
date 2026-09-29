using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Helpers;
using FluentAssertions;

namespace CheckYourEligibility.API.Tests.Helpers
{
    [TestFixture]
    public class WorkingFamiliesCheckHelperTests
    {

        [TestCase("2026-01-02", true)]
        [TestCase("2024-01-02", false)]
        public void IsChildTooYoung_expected_result(string dob, bool ischildTooYoung)
        {

            var checkDate = new DateTime(2026, 9, 9);
            var result = WorkingFamiliesCheckHelper.ChildIsTooYoung(DateTime.Parse(dob), checkDate);

            result.Should().Be(ischildTooYoung);
        }

        [TestCase("2025-01-01", "2025-01-01", false)]
        [TestCase("2026-01-14", "2025-12-31", true)]
        [TestCase("invalid", "2025-12-31", null)]
        public void IsDiscretionaryValidityStartDateApplied_expected_result(string dvsd, string vsd, bool? dvsdIsApplied)
        {
            // Act
            var result = WorkingFamiliesCheckHelper.IsDiscretionaryValidityStartDateApplied(
                vsd,
                dvsd);
            // Assert
            result.Should().Be(dvsdIsApplied);
        }

        [TestCaseSource(nameof(DetermineWorkingFamiliesCodeEligibilityCases))]
        public void DetermineWorkingFamiliesCodeEligibility_expected_status(
            string source,
            DateTime discretionaryValidityStartDate,
            DateTime validityEndDate,
            DateTime? gracePeriodEndDate,
            bool gracePeriodEndDateApplied,
            CheckEligibilityStatus expectedStatus)
        {
            var result = WorkingFamiliesCheckHelper.DetermineWorkingFamiliesCodeEligibility(
                source,
                discretionaryValidityStartDate,
                validityEndDate,
                gracePeriodEndDate,
                gracePeriodEndDateApplied);

            result.Should().Be(expectedStatus);
        }

        [TestCase("123456789", EligibilityCodeType.Temporary)]
        [TestCase("423456789", EligibilityCodeType.Foster)]
        [TestCase("523456789", EligibilityCodeType.Standard)]
        public void GetEligibilityCodeType_expected_result(string code, EligibilityCodeType type)
        {
            var result = WorkingFamiliesCheckHelper.GetEligibilityCodeType(code);

            result.Should().Be(type);
        }

        [TestCaseSource(nameof(SetTermValidityCases))]
        public void SetTermValidity_expected_result(
            DateTime checkDate,
            string gracePeriodEndDate,
            string validityStartDate,
            string childDob,
            Term expectedCurrentTerm,
            Term expectedNextTerm)
        {
            // Act
            var result = WorkingFamiliesCheckHelper.SetTermValidity(
                checkDate,
                gracePeriodEndDate,
                validityStartDate,
                childDob);

            // Assert
            result.Current.Name.Should().Be(expectedCurrentTerm.Name);
            result.Next.Name.Should().Be(expectedNextTerm.Name);
        }

        [TestCaseSource(nameof(SetReconfirmationPropertiesCases))]
        public void SetReconfirmationProperties_expected_status(
           string validityEndDate,
           string gracePeriodEndDate,
           DateTime checkDate,
           EligibilityCodeType codeType,
           string childDob,
           ReconfirmationProperties properties)
        {
            // Act
            var result = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
                validityEndDate,
                gracePeriodEndDate,
                checkDate,
                codeType,
                childDob);

            // Assert
            result.Status.Should().Be(properties.Status);
        }

        private static readonly DateTime ContiguousChainEvaluationDate = new(2026, 9, 29);

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_singleEventWithinCurrentTerm_returnsEventWithoutApplyingGracePeriod()
        {
            var eventRecord = CreateEvent(
                new DateTime(2026, 9, 2),
                new DateTime(2026, 9, 3),
                new DateTime(2026, 9, 2),
                new DateTime(2026, 9, 3));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([eventRecord]);

            result.Should().BeSameAs(eventRecord);
            gracePeriodEndDateApplied.Should().BeFalse();
        }

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_singleEventOutsideCurrentTerm_appliesGracePeriod()
        {
            var eventRecord = CreateEvent(
                new DateTime(2026, 8, 30),
                new DateTime(2026, 8, 31),
                new DateTime(2026, 8, 30),
                new DateTime(2026, 8, 31));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([eventRecord]);

            result.Should().BeSameAs(eventRecord);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_twoEventsSeparatedByValidityEnd_returnsLatestEventUnchanged()
        {
            var latestEvent = CreateEvent(
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 10),
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 10));
            var historicEvent = CreateEvent(
                new DateTime(2026, 6, 10),
                new DateTime(2026, 6, 30),
                new DateTime(2026, 6, 10),
                new DateTime(2026, 7, 2));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(new DateTime(2026, 7, 1));
            result.DiscretionaryValidityStartDate.Should().Be(new DateTime(2026, 7, 1));
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_twoContiguousEvents_returnsEarliestStartDates()
        {
            var latestEvent = CreateEvent(
                new DateTime(2026, 6, 24),
                new DateTime(2026, 7, 10),
                new DateTime(2026, 6, 24),
                new DateTime(2026, 7, 10));
            var historicEvent = CreateEvent(
                new DateTime(2026, 5, 20),
                new DateTime(2026, 6, 25),
                new DateTime(2026, 5, 20),
                new DateTime(2026, 6, 26));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(historicEvent.ValidityStartDate);
            result.ValidityEndDate.Should().Be(latestEvent.ValidityEndDate);
            result.GracePeriodEndDate.Should().Be(latestEvent.GracePeriodEndDate);
            result.DiscretionaryValidityStartDate.Should().Be(historicEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_multipleEventsSeparatedByGracePeriod_returnsLatestEventUnchanged()
        {
            var latestEvent = CreateEvent(
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 10),
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 10));
            var historicEvent = CreateEvent(
                new DateTime(2026, 6, 10),
                new DateTime(2026, 6, 20),
                new DateTime(2026, 6, 10),
                new DateTime(2026, 6, 30));
            var earlierEvent = CreateEvent(
                new DateTime(2026, 5, 10),
                new DateTime(2026, 5, 20),
                new DateTime(2026, 5, 10),
                new DateTime(2026, 5, 25));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent, earlierEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(new DateTime(2026, 7, 1));
            result.DiscretionaryValidityStartDate.Should().Be(new DateTime(2026, 7, 1));
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_chainBreakAfterContiguousPair_returnsStartOfContiguousBlock()
        {
            var latestEvent = CreateEvent(
                new DateTime(2026, 6, 24),
                new DateTime(2026, 7, 10),
                new DateTime(2026, 6, 24),
                new DateTime(2026, 7, 10));
            var contiguousEvent = CreateEvent(
                new DateTime(2026, 5, 20),
                new DateTime(2026, 6, 25),
                new DateTime(2026, 5, 20),
                new DateTime(2026, 6, 26));
            var earlierEvent = CreateEvent(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 20),
                new DateTime(2026, 4, 10),
                new DateTime(2026, 5, 1));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, contiguousEvent, earlierEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(contiguousEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(contiguousEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        [Test]
        public void CalculateContiguousChainForCodeFromEvents_allEventsContiguous_returnsEarliestStartDates()
        {
            var latestEvent = CreateEvent(
                new DateTime(2026, 6, 24),
                new DateTime(2026, 7, 10),
                new DateTime(2026, 6, 24),
                new DateTime(2026, 7, 10));
            var middleEvent = CreateEvent(
                new DateTime(2026, 5, 20),
                new DateTime(2026, 6, 25),
                new DateTime(2026, 5, 20),
                new DateTime(2026, 6, 26));
            var earliestEvent = CreateEvent(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 5, 21),
                new DateTime(2026, 4, 10),
                new DateTime(2026, 5, 22));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, middleEvent, earliestEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(earliestEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(earliestEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        private static WorkingFamiliesEvent CreateEvent(
            DateTime validityStartDate,
            DateTime validityEndDate,
            DateTime discretionaryValidityStartDate,
            DateTime gracePeriodEndDate)
        {
            return new WorkingFamiliesEvent
            {
                ValidityStartDate = validityStartDate,
                ValidityEndDate = validityEndDate,
                DiscretionaryValidityStartDate = discretionaryValidityStartDate,
                GracePeriodEndDate = gracePeriodEndDate
            };
        }

        #region Test Cases
        /// <summary>
        /// Term validity test cases
        /// </summary>
        /// <returns></returns>
        
        private static Term Term_Summer = new Term(TermName.Summer, new DateTime(DateTime.Now.Year, 4, 1), new DateTime(DateTime.Now.Year, 8, 31));

        private static Term Term_Spring = new Term(TermName.Spring, new DateTime(DateTime.Now.Year, 1, 1), new DateTime(DateTime.Now.Year, 3, 31));

        private static Term Term_Autumn = new Term(TermName.Autumn, new DateTime(DateTime.Now.Year, 9, 1), new DateTime(DateTime.Now.Year, 12, 31));

        private static IEnumerable<TestCaseData> DetermineWorkingFamiliesCodeEligibilityCases()
        {
            var today = DateTime.UtcNow.Date;

            yield return new TestCaseData(
                "childcare-admin",
                today.AddDays(-1),
                today,
                today.AddDays(-1),
                false,
                CheckEligibilityStatus.eligible)
                .SetArgDisplayNames("InternalSite_Uses_ValidityEndDate_When_GracePeriodNotApplied");

            yield return new TestCaseData(
                "childcare-admin",
                today.AddDays(-1),
                today.AddDays(-1),
                today.AddDays(1),
                false,
                CheckEligibilityStatus.notEligible)
                .SetArgDisplayNames("InternalSite_IsNotEligible_After_ValidityEndDate");

            yield return new TestCaseData(
                "childcare-admin",
                today.AddDays(-1),
                today.AddDays(-1),
                today,
                true,
                CheckEligibilityStatus.eligible)
                .SetArgDisplayNames("InternalSite_Uses_GracePeriod_When_Applied");

            yield return new TestCaseData(
                "client",
                today.AddDays(-1),
                today.AddDays(-1),
                today,
                false,
                CheckEligibilityStatus.eligible)
                .SetArgDisplayNames("ClientSite_Uses_GracePeriodEndDate");

            yield return new TestCaseData(
                "client",
                today.AddDays(1),
                today.AddDays(2),
                today.AddDays(3),
                false,
                CheckEligibilityStatus.notEligible)
                .SetArgDisplayNames("NotEligible_Before_DiscretionaryValidityStartDate");

            yield return new TestCaseData(
                "client",
                today.AddDays(-1),
                today.AddDays(1),
                null,
                false,
                CheckEligibilityStatus.notEligible)
                .SetArgDisplayNames("NotEligible_When_GracePeriodEndDate_IsMissing");
        }

        private static IEnumerable<TestCaseData> SetTermValidityCases()
        {
            yield return new TestCaseData(
                new DateTime(2025, 5, 1),     // check date
                "2025-12-31",                 // GPED
                "2024-01-01",                 // VSD
                "2018-01-01",                 // DOB - too old
                Term.None,
                Term.None).SetArgDisplayNames("None_When_Child_Too_Old");


            yield return new TestCaseData(
                new DateTime(2025, 10, 1),
                "2025-01-01",                 // GPED expired
                "2024-01-01",
                "2023-01-01",
                Term.None,
                Term.None)
                .SetArgDisplayNames("None_When_Grace_Period_Expired");
            //
            yield return new TestCaseData(
                new DateTime(2025, 2, 1),     // Spring term
                "2025-12-31",
                "2025-01-20",                 // VSD within current term
                "2023-01-01",
                Term.None,
                Term_Summer)
                .SetArgDisplayNames("Next_Term_When_VSD_In_Current_Term");

            yield return new TestCaseData(
                new DateTime(2026, 5, 1),     // Summer term
                "2026-12-31",                 // GPED beyond Autumn start
                "2026-01-01",
                "2023-01-01",
                Term_Summer,
                Term_Autumn)
                .SetArgDisplayNames("Current_And_Next_Term");

            yield return new TestCaseData(
                new DateTime(2026, 5, 1),     // Summer term
                "2026-08-31",                 // GPED before Autumn start
                "2024-01-01",
                "2024-01-01",
                Term_Summer,
                Term.None)
                .SetArgDisplayNames("Current_Term_Only");

            yield return new TestCaseData(
              DateTime.Today,
              "invalid",
              "invalid",
              "invalid",
              Term.None,
              Term.None)
              .SetArgDisplayNames("Invalid_Dates");
        }
        /// <summary>
        /// Reconfirmation properties test cases
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<TestCaseData> SetReconfirmationPropertiesCases()
        {


            yield return new TestCaseData(
                "2025-12-31",                       // VED
                "2026-03-31",                       // GPED
                new DateTime(2025, 6, 1),           // Check Date
                EligibilityCodeType.Standard,       // Code Type
                "2018-01-01",                       // Child DOB
                new ReconfirmationProperties()
                {
                    Status = ReconfirmationStatus.ChildTooOld
                })
                .SetArgDisplayNames("ChildTooOld");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2025, 6, 1),
                EligibilityCodeType.Temporary,
                "2022-01-01",
                  new ReconfirmationProperties()
                  {
                      Status = ReconfirmationStatus.NotApplicable
                  })
                .SetArgDisplayNames("NotApplicable_For_Temporary_Code");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2025, 11, 1),
                EligibilityCodeType.Standard,
                "2022-01-01",
                new ReconfirmationProperties()
                {
                    Status = ReconfirmationStatus.NotDueYet,
                    StartDate = DateTime.Parse("2025-12-03"),
                    EndDate = DateTime.Parse("2025-12-31")
                })
                .SetArgDisplayNames("NotDueYet");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2025, 12, 10),
                EligibilityCodeType.Standard,
                "2022-01-01",
                new ReconfirmationProperties()
                {
                    Status = ReconfirmationStatus.Due,
                    StartDate = DateTime.Parse("2025-12-03"),
                    EndDate = DateTime.Parse("2025-12-31")
                })
                .SetArgDisplayNames("Due");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2026, 1, 10),
                EligibilityCodeType.Standard,
                "2022-01-01",
                new ReconfirmationProperties()
                {
                    Status = ReconfirmationStatus.Overdue,
                    StartDate = DateTime.Parse("2025-12-03"),
                    EndDate = DateTime.Parse("2025-12-31")
                })
                .SetArgDisplayNames("Overdue");
            yield return new TestCaseData(
                "invalid",
                "invalid",
                DateTime.Today,
                EligibilityCodeType.Standard,
                "invalid",
                new ReconfirmationProperties
                {
                    Status = ReconfirmationStatus.NotApplicable
                })
                .SetArgDisplayNames("Invalid_Dates");
        }

        #endregion
    }
}