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

        [TestCase(2026, 1, 15, TermName.Spring, TermName.Summer)]
        [TestCase(2026, 4, 1, TermName.Summer, TermName.Autumn)]
        [TestCase(2026, 9, 1, TermName.Autumn, TermName.Spring)]
        public void GetTerms_expected_result(int year, int month, int day, TermName expectedCurrent, TermName expectedNext)
        {
            var date = new DateTime(year, month, day);

            var (current, next) = WorkingFamiliesCheckHelper.GetTerms(date);

            current.Name.Should().Be(expectedCurrent);
            next.Name.Should().Be(expectedNext);
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
                childDob, true);

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
        /// <summary>Verifies a single event within the the same term is returned without applying the grace period.</summary>
        [Test]
        public void IsGracePeriodEndDateApplie_singleEventWithinTheSameTerm_returnsEventWithoutApplyingGracePeriod()
        {
            var currentTerm = WorkingFamiliesCheckHelper.GetTerms(DateTime.UtcNow.Date).Current;

            var previousTerm =  WorkingFamiliesCheckHelper.GetTerms(currentTerm.StartDate.AddDays(-1)).Current;

            var result = WorkingFamiliesCheckHelper.isGracePeriodEndDateApplied(
                validityStartDate: previousTerm.StartDate,
                validityEndDdate: previousTerm.EndDate,
                eventCount: 1);

            result.Should().BeFalse();
        }

        /// <summary>Verifies a single event within the the same term is returned with gracePeriodEndDateApplied false but still returning the calculated date</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_singleEventWithinTheSameTerm_returnsEventWithoutApplyingGracePeriod()
        {
            var eventRecord = CreateEvent(
                validityStartDate: new DateTime(2026, 9, 2),
                validityEndDate: new DateTime(2026, 9, 3),
                discretionaryValidityStartDate: new DateTime(2026, 9, 2));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([eventRecord]);

            result.Should().BeSameAs(eventRecord);
            result.GracePeriodEndDate.Should().Be(new DateTime(2026, 12, 31));
            gracePeriodEndDateApplied.Should().BeFalse();
        }

        [TestCase(-1, -1)] // DVSD before term start, VED in current term
        [TestCase(5, 1)]  // DVSD in current term, VED after term end
        public void CalculateContiguousChainForCodeFromEvents_singleEventNotWithinTheSameTerm_GracePeriodScenarios( int dvsdOffsetFromTermStart, int vedOffset)
        {
            var currentTerm = WorkingFamiliesCheckHelper.GetTerms(DateTime.UtcNow.Date).Current;

            var dvsd = currentTerm.StartDate.AddDays(dvsdOffsetFromTermStart);

            var ved = vedOffset < 0 ? currentTerm.EndDate.AddDays(vedOffset) : currentTerm.EndDate.AddDays(vedOffset);

            var eventRecord = CreateEvent(
                validityStartDate: dvsd,
                validityEndDate: ved,
                discretionaryValidityStartDate: dvsd);

            var (_, gracePeriodApplied) = WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([eventRecord]);

            gracePeriodApplied.Should().BeTrue();
        }
        /// <summary>Verifies a single event extending beyond the current term has its grace period applied.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_singleEventSpanningPastCurrentTerm_appliesGracePeriod()
        {
            var currentTerm = WorkingFamiliesCheckHelper.GetTerms(DateTime.UtcNow.Date).Current;
            var eventRecord = CreateEvent(
                validityStartDate: currentTerm.StartDate,
                validityEndDate: currentTerm.EndDate.AddDays(1),
                discretionaryValidityStartDate: currentTerm.StartDate);

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([eventRecord]);

            result.Should().BeSameAs(eventRecord);
            gracePeriodEndDateApplied.Should().BeTrue();
        }
        /// <summary>Verifies third event does not reconnect with previously borken chain</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_ThirdEventMustNotReconnectExcludedFirstEvent()
        {

            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 11, 10),
                validityEndDate: new DateTime(2027, 2, 10),
                discretionaryValidityStartDate: new DateTime(2026, 11, 10),
                submissionDate: new DateTime(2026, 11, 10));
            var lateReconfirmation = CreateEvent(
                validityStartDate: new DateTime(2026, 9, 2),
                validityEndDate: new DateTime(2026, 12, 2),
                discretionaryValidityStartDate: new DateTime(2026, 9, 2),
                submissionDate: new DateTime(2026, 9, 2));
            var firstEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 1),
                validityEndDate: new DateTime(2026, 7, 31),
                discretionaryValidityStartDate: new DateTime(2026, 5, 1),
                submissionDate: new DateTime(2026, 5, 1));


            var (result, _) = WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents( [latestEvent, lateReconfirmation, firstEvent]);

       
            result.ValidityStartDate.Should().Be(new DateTime(2026, 9, 2));
            result.DiscretionaryValidityStartDate.Should().Be(new DateTime(2026, 9, 2));
        }


        /// <summary>Verifies a later submission after a same-term historic validity end breaks a two-event chain.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_twoEventsSeparatedByValidityEnd_ShouldBreakChain()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 30),
                validityEndDate: new DateTime(2026, 7, 10),
                discretionaryValidityStartDate: new DateTime(2026, 6, 30),
                submissionDate: new DateTime(2026, 7, 1));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 10),
                validityEndDate: new DateTime(2026, 6, 30),
                discretionaryValidityStartDate: new DateTime(2026, 6, 10));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(latestEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(latestEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        /// <summary>Verifies a later validity start alone does not break the chain when submission is not after historic VED.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_submissionNotAfterValidityEnd_keepsChainWhenValidityStartIsAfter()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 7, 1),
                validityEndDate: new DateTime(2026, 10, 1),
                discretionaryValidityStartDate: new DateTime(2026, 7, 1),
                submissionDate: new DateTime(2026, 6, 30));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 10),
                validityEndDate: new DateTime(2026, 6, 30),
                discretionaryValidityStartDate: new DateTime(2026, 6, 10));

            var (result, _) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(historicEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(historicEvent.DiscretionaryValidityStartDate);
        }

        /// <summary>Verifies a same-term late reconfirmation starts a three-event chain at the second event.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_lateReconfirmationBreakPreservedWithAdditionalEvent_keepsOriginalBreak()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 9, 2),
                validityEndDate: new DateTime(2026, 10, 1),
                discretionaryValidityStartDate: new DateTime(2026, 8, 1),
                submissionDate: new DateTime(2026, 10, 2));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 10),
                validityEndDate: new DateTime(2026, 6, 28),
                discretionaryValidityStartDate: new DateTime(2026, 5, 10),
                submissionDate: new DateTime(2026, 6, 15));
            var earlierEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 1, 1),
                validityEndDate: new DateTime(2026, 1, 10),
                discretionaryValidityStartDate: new DateTime(2026, 1, 1));

            var (result, _) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent, earlierEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(historicEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(historicEvent.DiscretionaryValidityStartDate);
        }

        /// <summary>Verifies contiguous events retain the earliest validity and discretionary start dates.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_twoContiguousEvents_returnsEarliestStartDates()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 24),
                validityEndDate: new DateTime(2026, 7, 10),
                discretionaryValidityStartDate: new DateTime(2026, 6, 24));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 20),
                validityEndDate: new DateTime(2026, 6, 25),
                discretionaryValidityStartDate: new DateTime(2026, 5, 20));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(historicEvent.ValidityStartDate);
            result.ValidityEndDate.Should().Be(latestEvent.ValidityEndDate);
            result.GracePeriodEndDate.Should().Be(latestEvent.GracePeriodEndDate);
            result.DiscretionaryValidityStartDate.Should().Be(historicEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        /// <summary>Verifies an event starting exactly on historic GPED is included in the contiguous chain.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_contiguousAtGracePeriodBoundary_includesHistoricDates()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 8, 31),
                validityEndDate: new DateTime(2027, 1, 31),
                discretionaryValidityStartDate: new DateTime(2026, 8, 31),
                submissionDate: new DateTime(2026, 6, 24));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 20),
                validityEndDate: new DateTime(2026, 6, 24),
                discretionaryValidityStartDate: new DateTime(2026, 5, 20));

            var (result, _) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(historicEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(historicEvent.DiscretionaryValidityStartDate);
        }

        /// <summary>Verifies a latest event after historic GPED is returned without pulling in older dates.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_multipleEventsSeparatedByGracePeriod_returnsLatestEventUnchanged()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2027, 1, 1),
                validityEndDate: new DateTime(2027, 4, 10),
                discretionaryValidityStartDate: new DateTime(2027, 1, 1));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 10),
                validityEndDate: new DateTime(2026, 6, 20),
                discretionaryValidityStartDate: new DateTime(2026, 6, 10));
            var earlierEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 10),
                validityEndDate: new DateTime(2026, 5, 20),
                discretionaryValidityStartDate: new DateTime(2026, 5, 10));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent, earlierEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(new DateTime(2027, 1, 1));
            result.DiscretionaryValidityStartDate.Should().Be(new DateTime(2027, 1, 1));
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        /// <summary>Verifies a chain break after a contiguous pair retains the start of that contiguous block.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_chainBreakAfterContiguousPair_returnsStartOfContiguousBlock()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 24),
                validityEndDate: new DateTime(2026, 7, 10),
                discretionaryValidityStartDate: new DateTime(2026, 6, 24));
            var contiguousEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 20),
                validityEndDate: new DateTime(2026, 6, 25),
                discretionaryValidityStartDate: new DateTime(2026, 5, 20));
            var earlierEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 2, 1),
                validityEndDate: new DateTime(2026, 2, 10),
                discretionaryValidityStartDate: new DateTime(2026, 2, 1));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, contiguousEvent, earlierEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(contiguousEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(contiguousEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        /// <summary>Verifies a non-contiguous first pair leaves the latest event's start dates unchanged.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_firstPairIsNotContiguous_keepsLatestDates()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 15),
                validityEndDate: new DateTime(2026, 7, 10),
                discretionaryValidityStartDate: new DateTime(2027, 1, 1));
            var historicEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 1),
                validityEndDate: new DateTime(2026, 6, 20),
                discretionaryValidityStartDate: new DateTime(2026, 5, 1));
            var earlierEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 4, 1),
                validityEndDate: new DateTime(2026, 5, 10),
                discretionaryValidityStartDate: new DateTime(2026, 4, 1));

            var (result, gracePeriodEndDateApplied) =
                WorkingFamiliesCheckHelper.CalculateContiguousChainForCodeFromEvents([latestEvent, historicEvent, earlierEvent]);

            result.Should().BeSameAs(latestEvent);
            result.ValidityStartDate.Should().Be(latestEvent.ValidityStartDate);
            result.DiscretionaryValidityStartDate.Should().Be(latestEvent.DiscretionaryValidityStartDate);
            gracePeriodEndDateApplied.Should().BeTrue();
        }

        /// <summary>Verifies an entirely contiguous event history uses the earliest event's start dates.</summary>
        [Test]
        public void CalculateContiguousChainForCodeFromEvents_allEventsContiguous_returnsEarliestStartDates()
        {
            var latestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 6, 24),
                validityEndDate: new DateTime(2026, 7, 10),
                discretionaryValidityStartDate: new DateTime(2026, 6, 24));
            var middleEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 5, 20),
                validityEndDate: new DateTime(2026, 6, 25),
                discretionaryValidityStartDate: new DateTime(2026, 5, 20));
            var earliestEvent = CreateEvent(
                validityStartDate: new DateTime(2026, 4, 10),
                validityEndDate: new DateTime(2026, 5, 21),
                discretionaryValidityStartDate: new DateTime(2026, 4, 10));

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
            DateTime? submissionDate = null)
        {
            return new WorkingFamiliesEvent
            {
                SubmissionDate = submissionDate ?? validityStartDate,
                ValidityStartDate = validityStartDate,
                ValidityEndDate = validityEndDate,
                DiscretionaryValidityStartDate = discretionaryValidityStartDate,
                GracePeriodEndDate = WorkingFamiliesEventHelper.GetGracePeriodEndDate(validityEndDate)
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