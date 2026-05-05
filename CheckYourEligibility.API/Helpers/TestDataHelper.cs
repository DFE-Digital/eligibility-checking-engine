using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.Helpers
{
    public static class TestDataHelper
    {
        public static (CheckEligibilityStatus, EligibilityTier?) TestDataCheck(string? nino, string? nass, CheckEligibilityType checkType, IConfiguration configuration)
        {

            if (!nino.IsNullOrEmpty())
            {
                if (checkType == CheckEligibilityType.FreeSchoolMeals && nino.StartsWith(configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleTargeted")))
                    return (CheckEligibilityStatus.eligible, EligibilityTier.targeted);

                if (checkType == CheckEligibilityType.FreeSchoolMeals && nino.StartsWith(configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleExpanded")))
                    return (CheckEligibilityStatus.eligible, EligibilityTier.expanded);

                if (nino.StartsWith(configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Eligible")))
                    return (CheckEligibilityStatus.eligible, null);
                if (nino.StartsWith(
                        configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:NotEligible")))
                    return (CheckEligibilityStatus.notEligible, null);
                if (nino.StartsWith(
                        configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:ParentNotFound")))
                    return (CheckEligibilityStatus.parentNotFound, null);
                if (nino.StartsWith(configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Error")))
                    return (CheckEligibilityStatus.error, null);

            }
            else
            {
                nass = nass.Substring(2, 2);
                if (nass == configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Eligible"))
                    return (CheckEligibilityStatus.eligible, null);
                if (nass == configuration.GetValue<string>(
                        "TestData:Outcomes:NationalAsylumSeekerServiceNumber:NotEligible"))
                    return (CheckEligibilityStatus.notEligible, null);
                if (nass == configuration.GetValue<string>(
                        "TestData:Outcomes:NationalAsylumSeekerServiceNumber:ParentNotFound"))
                    return (CheckEligibilityStatus.parentNotFound, null);
                if (nass == configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Error"))
                    return (CheckEligibilityStatus.error, null);
            }

            return (CheckEligibilityStatus.parentNotFound, null);
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
        /// <param name="checkData"></param>
        /// <returns></returns>
        public static async Task<WorkingFamiliesEvent> Generate_Test_Working_Families_EventRecord(CheckProcessData checkData, IConfiguration configuration)
        {

            string isEligiblePrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible");
            string isInGracePeriodPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:InGracePeriod");
            string isNotYetEligiblePrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:NotYetEligible");
            string isExpiredPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Expired");

            string eligibilityCode = checkData.EligibilityCode;
            WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent();

            // Parse date offsets from eligibility code
            int.TryParse(eligibilityCode.Substring(3, 2), out var vsdOffset);
            int.TryParse(eligibilityCode.Substring(5, 2), out var vedOffset);
            int.TryParse(eligibilityCode.Substring(7, 2), out var gpedOffset);

            // Apply date offsets based on scenario type
            if (!isEligiblePrefix.IsNullOrEmpty() && eligibilityCode.StartsWith(isEligiblePrefix))
            {
                wfEvent.ValidityStartDate = DateTime.Today.AddDays(-vsdOffset);
                wfEvent.ValidityEndDate = DateTime.Today.AddDays(vedOffset);
                wfEvent.GracePeriodEndDate = wfEvent.ValidityEndDate.AddDays(gpedOffset);
            }
            else if (!isInGracePeriodPrefix.IsNullOrEmpty() && eligibilityCode.StartsWith(isInGracePeriodPrefix))
            {
                wfEvent.ValidityEndDate = DateTime.Today.AddDays(-vedOffset);
                wfEvent.ValidityStartDate = wfEvent.ValidityEndDate.AddDays(-vsdOffset);
                wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(gpedOffset);
            }
            else if (!isNotYetEligiblePrefix.IsNullOrEmpty() && eligibilityCode.StartsWith(isNotYetEligiblePrefix))
            {
                wfEvent.ValidityStartDate = DateTime.Today.AddDays(vsdOffset);
                wfEvent.ValidityEndDate = wfEvent.ValidityStartDate.AddDays(vedOffset);
                wfEvent.GracePeriodEndDate = wfEvent.ValidityEndDate.AddDays(gpedOffset);
            }
            else if (!isExpiredPrefix.IsNullOrEmpty() && eligibilityCode.StartsWith(isExpiredPrefix))
            {
                wfEvent.GracePeriodEndDate = DateTime.Today.AddDays(-gpedOffset);
                wfEvent.ValidityEndDate = wfEvent.GracePeriodEndDate.AddDays(-vedOffset);
                wfEvent.ValidityStartDate = wfEvent.ValidityEndDate.AddDays(-vsdOffset);
            }
            else
            {
                return null;
            }

            // Populate the rest of the test record
            wfEvent.DiscretionaryValidityStartDate = wfEvent.ValidityStartDate;
            wfEvent.SubmissionDate = wfEvent.ValidityStartDate;
            wfEvent.ParentLastName = checkData.LastName ?? "TESTER";
            wfEvent.EligibilityCode = eligibilityCode;

            return wfEvent;
        }
    }
}
