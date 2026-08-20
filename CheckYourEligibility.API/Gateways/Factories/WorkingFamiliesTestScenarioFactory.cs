using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Gateways.Factories.Helper;
using CheckYourEligibility.API.Helpers;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using Microsoft.IdentityModel.Tokens;
using static CheckYourEligibility.API.Helpers.WorkingFamiliesCheckHelper;

namespace CheckYourEligibility.API.Gateways.Factories
{
    public interface IWorkingFamiliesTestScenarioFactory
    {
        bool IsTestCase(string eligibilityCode);
        WorkingFamiliesEvent? GenerateTestScenarioClientSide(CheckProcessData checkData);

    }
    public class WorkingFamiliesTestScenarioFactory : IWorkingFamiliesTestScenarioFactory
    {
        private readonly TestDataConfiguration _testDataConfig;

        public WorkingFamiliesTestScenarioFactory(TestDataConfiguration testDataConfig, ILoggerFactory loggerFactory)
        {
            _testDataConfig = testDataConfig ?? throw new ArgumentNullException(nameof(testDataConfig));
        }

        public bool IsTestCase(string eligibilityCode)
        {
            if (string.IsNullOrEmpty(_testDataConfig.WFTestCodePrefix))
                return false;

            return eligibilityCode.StartsWith(_testDataConfig.WFTestCodePrefix);
        }
        /// <summary>
        /// This method is used for generating test data in runtime
        /// If code starts with 900 it will generate an event record that must return Eligible
        /// If code starts with 901 it will generate an event record that must return Eligible in grace period
        /// If code starts with 902 it will generate an event record that must return NotEligible as it has not reached VSD yet
        /// If code starts with 903 it will generate an event record that must return NotEligible as the GPED has passed
        /// If code starts with 904 it will generate an event record that must return NotFound
        /// If code starts with 905 it will generate an event record that must return Error
        /// </summary>
        public WorkingFamiliesEvent? GenerateTestScenarioClientSide(CheckProcessData checkData)
        {
            if (string.IsNullOrEmpty(checkData.EligibilityCode))
                return null;

            var eligibilityCode = checkData.EligibilityCode;
            var wfEvent = new WorkingFamiliesEvent();

            // Parse date offsets from eligibility code (positions 3-7)
            int.TryParse(eligibilityCode.Substring(3, 2), out var vsdOffset);
            int.TryParse(eligibilityCode.Substring(5, 2), out var vedOffset);
            int.TryParse(eligibilityCode.Substring(7, 2), out var gpedOffset);

            // Apply date offsets based on scenario type
            if (!_testDataConfig.EligiblePrefix.IsNullOrEmpty() &&
                eligibilityCode.StartsWith(_testDataConfig.EligiblePrefix))
            {
                wfEvent = CreateEligibleScenario(vsdOffset, vedOffset, gpedOffset);
            }
            else if (!_testDataConfig.InGracePeriodPrefix.IsNullOrEmpty() &&
                     eligibilityCode.StartsWith(_testDataConfig.InGracePeriodPrefix))
            {
                wfEvent = CreateInGracePeriodScenario(vsdOffset, vedOffset, gpedOffset);
            }
            else if (!_testDataConfig.NotYetEligiblePrefix.IsNullOrEmpty() &&
                     eligibilityCode.StartsWith(_testDataConfig.NotYetEligiblePrefix))
            {
                wfEvent = CreateNotYetEligibleScenario(vsdOffset, vedOffset, gpedOffset);
            }
            else if (!_testDataConfig.ExpiredPrefix.IsNullOrEmpty() &&
                     eligibilityCode.StartsWith(_testDataConfig.ExpiredPrefix))
            {
                wfEvent = CreateExpiredScenario(vsdOffset, vedOffset, gpedOffset);
            }
            else return null;

            // Populate common fields
            PopulateCommonFields(wfEvent, checkData);


            return wfEvent;
        }
        // TBC
        public WorkingFamiliesEvent? GenerateTestScenarioInternalSide(CheckProcessData checkData)
        {
            
            // Get terms
            var checkDate = DateTime.Today;
            var terms = GetTerms(checkDate);

            return null;


        }
        #region Private
        /// Generates random date within a range
        private static DateTime RandomDateGenerator(DateTime startDate, DateTime endDate)
        {
            var random = new Random();
            int range = (endDate - startDate).Days + 1;
            return startDate.AddDays(random.Next(range));
        }

        /// <summary>
        /// Creates an event with
        /// VSD before the current term
        /// VED in a range so GPED is less than current term's end date
        /// </summary>
        /// <param name="currentTerm"></param>
        /// <param name="checkDate"></param>
        /// <returns>Returns a working families event that is valid for the current term only</returns>
        private WorkingFamiliesEvent CreateValidThisTermOnly(Term currentTerm, DateTime checkDate)
        {

            WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
            // VSD must be before the start of the current term
            // Generates a random date between the start of the reconfirmation window and the start of the current term.
            wfEvent.ValidityStartDate = RandomDateGenerator(currentTerm.StartDate.AddDays(-28), currentTerm.StartDate);
            // VED must be in a range so the GPED falls in the current term
            wfEvent.ValidityEndDate = currentTerm.Name switch
            {
                TermName.Spring => RandomDateGenerator(new DateTime(currentTerm.StartDate.Year, 1, 1), new DateTime(currentTerm.StartDate.Year, 2, 9)),
                TermName.Summer => RandomDateGenerator(new DateTime(currentTerm.StartDate.Year, 2, 11), new DateTime(currentTerm.StartDate.Year, 5, 26)),
                TermName.Autumn => RandomDateGenerator(new DateTime(currentTerm.StartDate.Year, 5, 27), new DateTime(currentTerm.StartDate.Year, 10, 21)),
                _ => throw new NotImplementedException()
            };
            // calculate GPED using business logic
            wfEvent.GracePeriodEndDate = WorkingFamiliesEventHelper.GetGracePeriodEndDate(wfEvent.ValidityEndDate);
            return wfEvent;
        }
        /// <summary>
        /// Creates an event with
        /// VSD before the current term
        /// VED in a range so GPED is greater than current term's end date
        /// </summary>
        /// <param name="currentTerm"></param>
        /// <param name="checkDate"></param>
        /// <returns>Returns a working families event that is valid for the current and next term</returns>
        /// <exception cref="NotImplementedException"></exception>
        private WorkingFamiliesEvent CreateValidCurrentAndNextTerm(Term currentTerm, DateTime checkDate)
        {

            WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
            // VSD must be before the start of the current term
            // Generates a random date between the start of the reconfirmation window and the start of the current term.
            wfEvent.ValidityStartDate = RandomDateGenerator(currentTerm.StartDate.AddDays(-28), currentTerm.StartDate);
            // VED must be in a range so the GPED falls in the next term
            wfEvent.ValidityEndDate = currentTerm.Name switch
            {
                TermName.Spring => RandomDateGenerator(new DateTime(currentTerm.StartDate.Year, 2, 11), new DateTime(currentTerm.StartDate.Year, 5, 26)),
                TermName.Summer => RandomDateGenerator(new DateTime(currentTerm.StartDate.Year, 5, 27), new DateTime(currentTerm.StartDate.Year, 10, 21)),
                TermName.Autumn => RandomDateGenerator(new DateTime(currentTerm.StartDate.Year + 1, 1, 1), new DateTime(currentTerm.StartDate.Year + 1, 2, 9)),
                _ => throw new NotImplementedException()
            };

            // Calculate GPED using business logic
            wfEvent.GracePeriodEndDate = WorkingFamiliesEventHelper.GetGracePeriodEndDate(wfEvent.ValidityEndDate);
            return wfEvent;

        }
        /// <summary>
        /// VSD - set a date in the past, ensure it is before the VED
        /// VED must be less than the Check date and within range to calculate a GPED in the past using the business logic method
        /// </summary>
        /// <param name="checkDate"></param>
        /// <returns></returns>
        private WorkingFamiliesEvent CreateReconfirmationOverDue(DateTime checkDate)
        {
            int year = checkDate.Year;
            WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();

            // Check date = 1 Jan - 10 Feb 
            if (checkDate >= new DateTime(year, 1, 1) && checkDate <= new DateTime(year, 2, 10))
            {
                wfEvent.ValidityStartDate = new DateTime(year - 1,7, 31);
                // GPED business logic:
                // If validity end date between 1 September – 21 October - GPED = 31-Dec the same year
                wfEvent.ValidityEndDate = RandomDateGenerator(new DateTime(year - 1, 9, 1), new DateTime(year - 1, 10, 21));
            }
            // Check date 11 Feb - 26 May
            else if (checkDate >= new DateTime(year, 2, 11) && checkDate <= new DateTime(year, 5, 26))
            {
                wfEvent.ValidityStartDate = new DateTime(year - 1,10, 30);

                // GPED business logic:
                // If validity end date between 11 Jan – 10 Feb - GPED = 31-Dec the same year
                wfEvent.ValidityEndDate = RandomDateGenerator(new DateTime(year, 1, 11), new DateTime(year, 2, 10));
            }
            // Check date 27 May – 31 August
            else if (checkDate >= new DateTime(year, 5, 27) && checkDate <= new DateTime(year, 8, 31))
            {
                wfEvent.ValidityStartDate = new DateTime(year, 1, 1);
                // GPED business logic
                // If validity end date between 11 Feb – 26 May - GPED = 31-Aug the same year
                wfEvent.ValidityEndDate = RandomDateGenerator(new DateTime(year, 2, 11), new DateTime(year, 5, 26));
            }
            // Check date 1 September – 21 October
            else if (checkDate >= new DateTime(year, 9, 1) && checkDate <= new DateTime(year, 10, 21))
            {

                wfEvent.ValidityStartDate = new DateTime(year, 1, 20);
                // GPED business logic
                // If validity end date between 11 Feb– 26 May - GPED = 31-Aug the same year
                wfEvent.ValidityEndDate = RandomDateGenerator(new DateTime(year , 2, 11), new DateTime(year, 5, 26));
            }
            // Check date 22 October - 31 December
            else
            {
                wfEvent.ValidityStartDate = new DateTime(year, 3, 1);
                // GPED business logic
                // If validity end date between 1 May – 26 May - GPED = 31 - Aug the same year
                wfEvent.ValidityEndDate = RandomDateGenerator(new DateTime(year, 5, 1), new DateTime(year, 5, 26));
            }
            
            wfEvent.GracePeriodEndDate = WorkingFamiliesEventHelper.GetGracePeriodEndDate(wfEvent.ValidityEndDate);

            return wfEvent;
        }
        /// <summary>
        ///  VSD - vsd must be within the current term, either in the past of in the future of the checked date
        ///  VED - set it to 3months in the future from now
        /// </summary>
        /// <param name="currentTerm"></param>
        /// <param name="checkDate"></param>
        /// <returns></returns>
        private WorkingFamiliesEvent CreateValidCannotBeUsedYet(Term currentTerm , DateTime checkDate) {
            WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
            wfEvent.ValidityStartDate = currentTerm.StartDate.AddDays(15);
            wfEvent.ValidityEndDate = checkDate.AddMonths(3);
            wfEvent.GracePeriodEndDate = WorkingFamiliesEventHelper.GetGracePeriodEndDate(wfEvent.ValidityEndDate);

            return wfEvent;
        }
        /// <summary>
        /// VSD - one day before the start of the current term to ensure term validity is true
        /// VED - in the past
        /// </summary>
        /// <param name="currentTerm"></param>
        /// <param name="checkDate"></param>
        /// <returns></returns>
        private WorkingFamiliesEvent CreateInGracePeriod(Term currentTerm, DateTime checkDate) {
            
            WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();
            wfEvent.ValidityStartDate = currentTerm.StartDate.AddDays(-1);
           // tbc
           return wfEvent;

        }
        private WorkingFamiliesEvent CreateEligibleScenario(int vsdOffset, int vedOffset, int gpedOffset)
        {
            var today = DateTime.Today;
            return new WorkingFamiliesEvent
            {
                ValidityStartDate = today.AddDays(-vsdOffset),
                ValidityEndDate = today.AddDays(vedOffset),
                GracePeriodEndDate = today.AddDays(vedOffset).AddDays(gpedOffset)
            };
        }

        private WorkingFamiliesEvent CreateInGracePeriodScenario(int vsdOffset, int vedOffset, int gpedOffset)
        {
            var today = DateTime.Today;
            return new WorkingFamiliesEvent
            {
                ValidityEndDate = today.AddDays(-vedOffset),
                ValidityStartDate = today.AddDays(-vedOffset).AddDays(-vsdOffset),
                GracePeriodEndDate = today.AddDays(gpedOffset)
            };
        }

        private WorkingFamiliesEvent CreateNotYetEligibleScenario(int vsdOffset, int vedOffset, int gpedOffset)
        {
            var today = DateTime.Today;
            return new WorkingFamiliesEvent
            {
                ValidityStartDate = today.AddDays(vsdOffset),
                ValidityEndDate = today.AddDays(vsdOffset).AddDays(vedOffset),
                GracePeriodEndDate = today.AddDays(vsdOffset).AddDays(vedOffset).AddDays(gpedOffset)
            };
        }

        private WorkingFamiliesEvent CreateExpiredScenario(int vsdOffset, int vedOffset, int gpedOffset)
        {
            var today = DateTime.Today;
            return new WorkingFamiliesEvent
            {
                GracePeriodEndDate = today.AddDays(-gpedOffset),
                ValidityEndDate = today.AddDays(-gpedOffset).AddDays(-vedOffset),
                ValidityStartDate = today.AddDays(-gpedOffset).AddDays(-vedOffset).AddDays(-vsdOffset)
            };
        }

        private void PopulateCommonFields(WorkingFamiliesEvent wfEvent, CheckProcessData checkData)
        {
            wfEvent.DiscretionaryValidityStartDate = wfEvent.ValidityStartDate;
            wfEvent.SubmissionDate = wfEvent.ValidityStartDate;
            wfEvent.ParentLastName = checkData.LastName ?? "TESTER";
            wfEvent.EligibilityCode = checkData.EligibilityCode;
        }
        #endregion

    }
}