using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Factories.Helper;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.Gateways.Factories
{
    public interface IWorkingFamiliesTestScenarioFactory { }
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
        public WorkingFamiliesEvent? GenerateTestScenario(CheckProcessData checkData)
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

        #region Private
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