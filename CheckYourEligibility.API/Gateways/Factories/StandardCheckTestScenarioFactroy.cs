using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Factories.Helper;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.Gateways.Factories
{
    public interface IStandardCheckTestScenarioFactroy { }
    public class StandardCheckTestScenarioFactroy : IStandardCheckTestScenarioFactroy
    {
        private readonly TestDataConfiguration _testDataConfiguration;

        public StandardCheckTestScenarioFactroy(TestDataConfiguration testDataConfiguration)
        {
            _testDataConfiguration = testDataConfiguration;

        }
        public bool IsTestData(string? nino, string? nass) {

            if (!nino.IsNullOrEmpty())
            {
                return _testDataConfiguration.NinoTestScenarios.Keys.Any(prefix => nino.StartsWith(prefix));
            }

            if (!nass.IsNullOrEmpty())
            {
                var nassPrefix = nass.Substring(2, 2);
                return _testDataConfiguration.NassTestScenarios.Keys.Any(prefix => nassPrefix == prefix);
            }

            return false;
        
        }
        private (CheckEligibilityStatus, EligibilityTier?) TestDataCheck(string? nino, string? nass, CheckEligibilityType checkType)
        {

            if (!nino.IsNullOrEmpty())
            {
                if (checkType == CheckEligibilityType.FreeSchoolMeals && nino.StartsWith(_configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleTargeted")))
                    return (CheckEligibilityStatus.eligible, EligibilityTier.targeted);

                if (checkType == CheckEligibilityType.FreeSchoolMeals && nino.StartsWith(_configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleExpanded")))
                    return (CheckEligibilityStatus.eligible, EligibilityTier.expanded);

                if (nino.StartsWith(_configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Eligible")))
                    return (CheckEligibilityStatus.eligible, null);
                if (nino.StartsWith(
                        _configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:NotEligible")))
                    return (CheckEligibilityStatus.notEligible, null);
                if (nino.StartsWith(
                        _configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:ParentNotFound")))
                    return (CheckEligibilityStatus.parentNotFound, null);
                if (nino.StartsWith(_configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Error")))
                    return (CheckEligibilityStatus.error, null);

            }
            else
            {
                nass = nass.Substring(2, 2);
                if (nass == _configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Eligible"))
                    return (CheckEligibilityStatus.eligible, EligibilityTier.targeted);
                if (nass == _configuration.GetValue<string>(
                        "TestData:Outcomes:NationalAsylumSeekerServiceNumber:NotEligible"))
                    return (CheckEligibilityStatus.notEligible, null);
                if (nass == _configuration.GetValue<string>(
                        "TestData:Outcomes:NationalAsylumSeekerServiceNumber:ParentNotFound"))
                    return (CheckEligibilityStatus.parentNotFound, null);
                if (nass == _configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Error"))
                    return (CheckEligibilityStatus.error, null);
            }

            return (CheckEligibilityStatus.parentNotFound, null);
        }
    }
}
