using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Helpers;
using FluentAssertions;

namespace CheckYourEligibility.API.Tests.Helpers
{
    [TestFixture]
    public class WorkingFamiliesCheckHelperTests
    {


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
            TermName expectedCurrentTerm,
            TermName expectedNextTerm)
        {
            // Act
            var result = WorkingFamiliesCheckHelper.SetTermValidity(
                checkDate,
                gracePeriodEndDate,
                validityStartDate,
                childDob);

            // Assert
            result.Current.Should().Be(expectedCurrentTerm);
            result.Next.Should().Be(expectedNextTerm);
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
        #region Test Cases
        /// <summary>
        /// Term validity test cases
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<TestCaseData> SetTermValidityCases()
        {
            yield return new TestCaseData(
                new DateTime(2025, 5, 1),     // check date
                "2025-12-31",                 // GPED
                "2024-01-01",                 // VSD
                "2018-01-01",                 // DOB - too old
                TermName.None,
                TermName.None).SetArgDisplayNames("None_When_Child_Too_Old");
               

            yield return new TestCaseData(
                new DateTime(2025, 10, 1),
                "2025-01-01",                 // GPED expired
                "2024-01-01",
                "2023-01-01",
                TermName.None,
                TermName.None)
                .SetArgDisplayNames("None_When_Grace_Period_Expired");
            //
            yield return new TestCaseData(
                new DateTime(2025, 2, 1),     // Spring term
                "2025-12-31",
                "2025-01-20",                 // VSD within current term
                "2023-01-01",
                TermName.None,
                TermName.Summer)
                .SetArgDisplayNames("Next_Term_When_VSD_In_Current_Term");

            yield return new TestCaseData(
                new DateTime(2026, 5, 1),     // Summer term
                "2026-12-31",                 // GPED beyond Autumn start
                "2026-01-01",
                "2023-01-01",
                TermName.Summer,
                TermName.Autumn)
                .SetArgDisplayNames("Current_And_Next_Term");

            yield return new TestCaseData(
                new DateTime(2026, 5, 1),     // Summer term
                "2026-08-31",                 // GPED before Autumn start
                "2024-01-01",
                "2024-01-01",
                TermName.None,
                TermName.Summer)
                .SetArgDisplayNames("Current_Term_Only");

            yield return new TestCaseData(
              DateTime.Today,
              "invalid",
              "invalid",
              "invalid",
              TermName.None,
              TermName.None)
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
                    Status = ReconfirmationStatus.ChildTooOld,
                    StartDate = null,
                    EndDate = null
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
                      Status = ReconfirmationStatus.NotApplicable,
                      StartDate = null,
                      EndDate = null
                  })
                .SetArgDisplayNames("NotApplicable_For_Temporary_Code");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2025, 11, 1),
                EligibilityCodeType.Standard,
                "2022-01-01",
                new ReconfirmationProperties() { 
                    Status = ReconfirmationStatus.NotDueYet, 
                    StartDate = "2025-12-03",
                    EndDate = "2025-12-31"
                })
                .SetArgDisplayNames("NotDueYet");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2025, 12, 10),
                EligibilityCodeType.Standard,
                "2022-01-01",
                new ReconfirmationProperties() {
                    Status = ReconfirmationStatus.Due, 
                    StartDate = "2025-12-03",
                    EndDate = "2025-12-31" })
                .SetArgDisplayNames("Due");

            yield return new TestCaseData(
                "2025-12-31",
                "2026-03-31",
                new DateTime(2026, 1, 10),
                EligibilityCodeType.Standard,
                "2022-01-01",
                new ReconfirmationProperties() { 
                    Status = ReconfirmationStatus.Overdue, 
                    StartDate = "2025-12-03", 
                    EndDate = "2025-12-31" })
                .SetArgDisplayNames("Overdue");
            yield return new TestCaseData(
                "invalid",
                "invalid",
                DateTime.Today,
                EligibilityCodeType.Standard,
                "invalid",
                new ReconfirmationProperties
                {
                    Status = ReconfirmationStatus.NotApplicable,
                    StartDate = null,
                    EndDate = null
                })
                .SetArgDisplayNames("Invalid_Dates");
        }

        #endregion
    }
}